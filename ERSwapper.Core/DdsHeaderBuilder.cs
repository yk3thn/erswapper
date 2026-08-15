using System.Buffers.Binary;
using System.Text;

namespace ERSwapper.Core;

public static class DdsHeaderBuilder
{
    public const int HeaderSize = 128;

    public const int Dx10HeaderSize = 148;

    private const uint Magic = 0x20534444;

    private const uint DDSD_CAPS = 0x1;
    private const uint DDSD_HEIGHT = 0x2;
    private const uint DDSD_WIDTH = 0x4;
    private const uint DDSD_PIXELFORMAT = 0x1000;
    private const uint DDSD_MIPMAPCOUNT = 0x20000;
    private const uint DDSD_LINEARSIZE = 0x80000;

    private const uint DDPF_FOURCC = 0x4;

    private const uint DDSCAPS_COMPLEX = 0x8;
    private const uint DDSCAPS_TEXTURE = 0x1000;
    private const uint DDSCAPS_MIPMAP = 0x400000;

    public static byte[] BuildBc1Header(int width, int height, int mipCount)
    => BuildHeader("BC1_UNORM", width, height, mipCount);

    public static byte[] BuildHeader(string dxgiFormat, int width, int height, int mipCount)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Invalid texture dimensions {width}x{height}.");
        if (mipCount <= 0)
            throw new ArgumentException($"Invalid mip count {mipCount}.");

        string fourCc = FourCcForFormat(dxgiFormat);
        uint linearSize = BlockCountForFormat(dxgiFormat, width, height);

        byte[] header = new byte[HeaderSize];

        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset), value);

        U32(0, Magic);
        U32(4, 124);
        U32(8, DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT
              | DDSD_MIPMAPCOUNT | DDSD_LINEARSIZE);
        U32(12, (uint)height);
        U32(16, (uint)width);
        U32(20, linearSize);
        U32(24, 0);
        U32(28, (uint)mipCount);

        U32(76, 32);
        U32(80, DDPF_FOURCC);
        Encoding.ASCII.GetBytes(fourCc).CopyTo(header.AsSpan(84));
        U32(88, 0);
        U32(92, 0);
        U32(96, 0);
        U32(100, 0);
        U32(104, 0);

        U32(108, DDSCAPS_TEXTURE | DDSCAPS_COMPLEX | DDSCAPS_MIPMAP);

        return header;
    }

    public static byte[] StripHeader(byte[] dds, string expectedFormat)
    {
        if (dds.Length < HeaderSize)
            throw new InvalidDataException(
                $"File is {dds.Length} bytes — too short to be a .dds (needs at least {HeaderSize}).");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(dds);
        if (magic != Magic)
            throw new InvalidDataException("File does not start with the 'DDS ' magic — texconv output is not a DDS.");

        uint pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(80));
        string fourCc = Encoding.ASCII.GetString(dds, 84, 4);

        if ((pfFlags & DDPF_FOURCC) != 0 && fourCc == "DX10")
        {
            throw new InvalidDataException(
                $"texconv produced a DXT10-header DDS for format '{expectedFormat}'. " +
                "Only DX9 FourCC formats (BC1/BC2/BC3) can be written back into the .resS blob.");
        }

        if ((pfFlags & DDPF_FOURCC) == 0)
            throw new InvalidDataException(
                "texconv produced an uncompressed DDS. Expected a block-compressed format — " +
                $"check that '{expectedFormat}' is a valid texconv -f value.");

        string expectedFourCc = FourCcForFormat(expectedFormat);
        if (fourCc != expectedFourCc)
            throw new InvalidDataException(
                $"texconv produced a '{fourCc}' DDS but '{expectedFormat}' requires '{expectedFourCc}'. " +
                "Writing this would corrupt the bundle, so the operation was stopped.");

        byte[] payload = new byte[dds.Length - HeaderSize];
        Array.Copy(dds, HeaderSize, payload, 0, payload.Length);
        return payload;
    }

    public static uint BlockCountForFormat(string dxgiFormat, int width, int height)
    {
        int blockBytes = BlockBytesForFormat(dxgiFormat);
        if (blockBytes == 0) return 0;

        long blocksWide = Math.Max(1, (width + 3) / 4);
        long blocksHigh = Math.Max(1, (height + 3) / 4);
        return (uint)(blocksWide * blocksHigh * blockBytes);
    }

    public static long ComputeMipChainSize(string dxgiFormat, int width, int height, int mipCount)
    {
        int blockBytes = BlockBytesForFormat(dxgiFormat);
        if (blockBytes == 0) return 0;

        long total = 0;
        int w = width, h = height;

        for (int i = 0; i < mipCount; i++)
        {
            long blocksWide = Math.Max(1, (w + 3) / 4);
            long blocksHigh = Math.Max(1, (h + 3) / 4);
            total += blocksWide * blocksHigh * blockBytes;

            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
        }

        return total;
    }

    public static int InferMipCount(string dxgiFormat, int width, int height, long totalSize)
    {
        if (width <= 0 || height <= 0 || totalSize <= 0) return 0;
        if (BlockBytesForFormat(dxgiFormat) == 0) return 0;

        int maxMips = 1 + (int)Math.Log2(Math.Max(width, height));

        for (int mips = 1; mips <= maxMips; mips++)
        {
            if (ComputeMipChainSize(dxgiFormat, width, height, mips) == totalSize) return mips;
        }

        return 0;
    }

    public static int BlockBytesForFormat(string dxgiFormat)
    {
        return Normalize(dxgiFormat) switch
        {
            "BC1" => 8,
            "BC2" => 16,
            "BC3" => 16,
            _ => 0,
        };
    }

    private static string FourCcForFormat(string dxgiFormat)
    {
        return Normalize(dxgiFormat) switch
        {
            "BC1" => "DXT1",
            "BC2" => "DXT3",
            "BC3" => "DXT5",
            _ => throw new NotSupportedException(
                $"Format '{dxgiFormat}' cannot be written as a legacy DX9 DDS header. " +
                "Supported: BC1_UNORM (DXT1), BC2_UNORM (DXT3), BC3_UNORM (DXT5). " +
                "BC4/BC5/BC6H/BC7 require a DXT10 header, which this pipeline does not handle.")
        };
    }

    private static string Normalize(string dxgiFormat)
    {
        if (string.IsNullOrWhiteSpace(dxgiFormat)) return "";

        string f = dxgiFormat.Trim().ToUpperInvariant();

        if (f is "DXT1") return "BC1";
        if (f is "DXT3") return "BC2";
        if (f is "DXT5") return "BC3";

        if (f.StartsWith("BC1")) return "BC1";
        if (f.StartsWith("BC2")) return "BC2";
        if (f.StartsWith("BC3")) return "BC3";

        return f;
    }
}
