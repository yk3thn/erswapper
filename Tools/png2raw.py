import argparse
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

FORMATS = {
    "BC1_UNORM": ("DXT1", 8),
    "BC2_UNORM": ("DXT3", 16),
    "BC3_UNORM": ("DXT5", 16),
}

DDS_MAGIC = b"DDS "
DDS_HEADER_SIZE = 128

HELP_EPILOG = """
what this does
  Converts a PNG into the raw block-compressed bytes that live inside a Unity .resS blob,
  with a full mip chain and no header. Writes <name>.bin next to the PNG.

where to paste the result
  Editing the exported .resS   ->  paste at m_StreamData.offset exactly.
  Editing the .bundle          ->  paste at (offset of the .resS inside the bundle)
                                   + m_StreamData.offset.

  Always overwrite in place. Never insert. Textures sit back to back with no padding,
  so inserting shifts everything after it and ruins the rest of the blob.

size check
  The byte count must equal the target texture's m_StreamData.size. Pass --expect to have
  that checked for you; the script exits non-zero and tells you not to paste if it differs.

flip
  Unity stores texture rows bottom-up, so the image is flipped before compressing.
  Whatever tool you extracted with must agree:
    extracted image looked the right way up   ->  leave the flip on (default)
    extracted image looked upside down        ->  pass --no-flip
  Flipping on only one side puts the texture into the game upside down.

requirements
  texconv.exe from https://github.com/microsoft/DirectXTex/releases
  Found automatically next to this script, next to the PNG, or on PATH.
  Otherwise pass --texconv.

examples
  python png2raw.py texture.png
  python png2raw.py texture.png --expect 349544
  python png2raw.py texture.png --format BC3_UNORM --no-flip
"""


def find_texconv(explicit, png_path):
    if explicit:
        path = Path(explicit).expanduser()
        if not path.exists():
            raise RuntimeError(f"texconv.exe not found at: {path}")
        return str(path)

    for candidate in (
        Path(__file__).resolve().parent / "texconv.exe",
        png_path.parent / "texconv.exe",
    ):
        if candidate.exists():
            return str(candidate)

    found = shutil.which("texconv")
    if found:
        return found

    raise RuntimeError(
        "texconv.exe not found.\n"
        "Download it from https://github.com/microsoft/DirectXTex/releases and put it\n"
        "next to this script, next to your PNG, or on your PATH. Or pass --texconv <path>."
    )


def read_png_size(png_path):
    data = png_path.read_bytes()[:24]

    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        raise RuntimeError(f"{png_path.name} is not a PNG file.")

    return struct.unpack(">II", data[16:24])


def full_mip_count(width, height):
    levels = 1
    while width > 1 or height > 1:
        width = max(1, width // 2)
        height = max(1, height // 2)
        levels += 1
    return levels


def mip_chain_size(width, height, block_bytes, levels):
    total = 0
    for _ in range(levels):
        total += max(1, (width + 3) // 4) * max(1, (height + 3) // 4) * block_bytes
        width = max(1, width // 2)
        height = max(1, height // 2)
    return total


def run_texconv(texconv, png_path, work_dir, fmt, mips, flip):
    print(f"[3/5] Compressing to {fmt} ...")

    cmd = [texconv, "-nologo", "-f", fmt, "-m", str(mips), "-dx9", "-y"]
    if flip:
        cmd.append("-vflip")
    cmd += ["-o", str(work_dir), str(png_path)]

    result = subprocess.run(cmd, capture_output=True, text=True)

    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr, file=sys.stderr)
        raise RuntimeError("texconv failed, see the output above.")

    dds_path = work_dir / (png_path.stem + ".dds")
    if not dds_path.exists():
        raise RuntimeError("texconv reported success but produced no .dds file.")

    return dds_path


def strip_dds_header(dds_path, expected_fourcc):
    print(f"[4/5] Stripping the {DDS_HEADER_SIZE}-byte DDS header ...")

    data = dds_path.read_bytes()

    if len(data) <= DDS_HEADER_SIZE:
        raise RuntimeError("The DDS is smaller than its own header, conversion failed.")

    if data[:4] != DDS_MAGIC:
        raise RuntimeError("texconv output is not a DDS file.")

    fourcc = data[84:88].decode("ascii", errors="replace")

    if fourcc == "DX10":
        raise RuntimeError("Got a DX10-header DDS. Only BC1, BC2 and BC3 work here.")

    if fourcc != expected_fourcc:
        raise RuntimeError(f"Expected {expected_fourcc} but texconv produced {fourcc}.")

    return data[DDS_HEADER_SIZE:]


def main():
    parser = argparse.ArgumentParser(
        description="Convert a PNG into raw .resS bytes for manual hex editing.",
        epilog=HELP_EPILOG,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("png", help="source PNG")
    parser.add_argument("--format", default="BC1_UNORM", choices=sorted(FORMATS),
                        help="texture format, default BC1_UNORM (Unity m_TextureFormat 10)")
    parser.add_argument("--mips", type=int, default=0,
                        help="mip levels, 0 means a full chain (default)")
    parser.add_argument("--no-flip", action="store_true",
                        help="do not flip vertically, see the notes below")
    parser.add_argument("--expect", type=int, default=None,
                        help="the target texture's m_StreamData.size, checked against the result")
    parser.add_argument("--texconv", default=None, help="path to texconv.exe")
    parser.add_argument("-o", "--output", default=None, help="output .bin path")
    args = parser.parse_args()

    png_path = Path(args.png).expanduser().resolve()
    if not png_path.exists():
        raise RuntimeError(f"PNG not found: {png_path}")

    fourcc, block_bytes = FORMATS[args.format]
    texconv = find_texconv(args.texconv, png_path)

    print(f"[1/5] Reading {png_path.name} ...")
    width, height = read_png_size(png_path)

    levels = args.mips if args.mips > 0 else full_mip_count(width, height)

    print("[2/5] Working out the expected size ...")
    expected = mip_chain_size(width, height, block_bytes, levels)

    print(f"      {width} x {height}, {args.format} ({fourcc}), {levels} mip levels")
    print(f"      A correct payload is {expected:,} bytes")
    print(f"      Flip: {'no (--no-flip)' if args.no_flip else 'yes'}")

    work_dir = Path(tempfile.mkdtemp(prefix="png2raw_"))
    try:
        dds_path = run_texconv(texconv, png_path, work_dir, args.format, args.mips,
                               flip=not args.no_flip)
        raw = strip_dds_header(dds_path, fourcc)
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)

    out_path = Path(args.output).expanduser() if args.output else png_path.with_suffix(".bin")
    out_path.write_bytes(raw)

    print(f"[5/5] Wrote {out_path}")
    print()
    print(f"      Bytes produced : {len(raw):,}")
    print(f"      Expected       : {expected:,}")

    if len(raw) != expected:
        print()
        print("      MISMATCH. Do not paste this.")
        print("      Check the PNG size, the format and the mip count.")
        return 1

    print("      Size matches the dimensions.")

    if args.expect is None:
        print()
        print("      Check this equals the target's m_StreamData.size before pasting.")
        print("      Overwrite in place, never insert.")
        return 0

    print()
    print(f"      m_StreamData.size : {args.expect:,}")

    if len(raw) != args.expect:
        print("      MISMATCH against the texture you are replacing. Do not paste this,")
        print("      it would run over the next texture in the blob.")
        return 1

    print("      Matches the target texture. Safe to overwrite in place.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"\nERROR: {error}", file=sys.stderr)
        sys.exit(1)
