# ER Swapper

## What it actually does

Rust's textures live inside large `.bundle` files (Unity `UnityFS` containers). Inside each bundle
is a `.resS` blob — the raw block-compressed pixel data for every streamed texture in that bundle,
concatenated back to back with no padding.

Each texture object records:

- `m_StreamData.offset` — a byte offset **relative to the start of the `.resS` blob**
- `m_StreamData.size` / `m_CompleteImageSize` — its byte length
- width, height, mip count, texture format

To read or write one texture we need its **absolute** (asset) offset inside the physical `.bundle`:

```
absolute_offset = resS_entry_start_in_bundle + m_StreamData.offset
```

`resS_entry_start_in_bundle` is constant for a given bundle, so the app finds it once and caches it.

---

## First-run setup

### 1. texconv.exe

PNG ↔ DDS conversion is done by [texconv](https://github.com/microsoft/DirectXTex/releases)
(Microsoft DirectXTex) rather than a managed encoder, because it produces exactly the BC1 bit
layout the game shipped with.

### 2. Rust install folder

Auto-detected from Steam's registry entry and `libraryfolders.vdf`. Override in **Settings** if the
guess is wrong — it should be the folder containing `RustClient.exe`.

### 3. Bundle signatures

Nothing to do here — the signatures ship with the app, in `Config/Signatures/`. This is how the
`.resS` blob gets located, and it happens two ways.

**Structurally, first.** A `.bundle` is a `UnityFS` container with a directory listing every file it
holds and where each one starts. Reading that directory gives the `.resS` start exactly, with no
searching and no guessing. This works for every bundle the app ships items for.

**By signature, as a fallback.** If the directory can't be read — a compressed layout, or a format
change in a future Rust update — the app falls back to scanning the bundle for a *signature*: the
first 4 KB of a known-clean `.resS`. `Config/bundles.json` maps each bundle to its `.sig`.

Either way the answer is the same number, and it is cached in `offset_cache.json` so the work
happens once. The signature must match at exactly **one** offset; zero or multiple matches abort
rather than guess.

## The shipped app (ERSwapper)

A **loading screen** runs first and does everything slow up front — locating the texture data
inside the bundle and building item previews — so the gallery is complete the moment the main
window appears rather than filling in while the user is trying to use it.

The main window is a **category rail** (Weapons, Tools, Deployables…) beside a gallery of item
tiles. Pick a category, pick an item, then:

1. **Extract & Open in Editor** — writes the current texture to your Desktop as a PNG and opens it
2. Edit and save it, keeping the same dimensions
3. **Apply Edited Texture** — converts it back and patches it in, after backing up the game file

**Reset All Swaps** puts everything back. **What cannot be swapped?** lists the textures Rust stores
in a form this tool cannot rewrite. **Rebuild Previews** deletes the cached preview images and reads them from the
game again, for when a preview looks wrong or stale; it touches no game files and no swaps.

It ships self-contained: the item catalogue, the `.resS` signatures, pre-built thumbnails, the
offset cache and `texconv.exe` all live in the output folder.

## Workflow

1. Select an item.
2. **Extract & Open in Editor** — finds the `.resS` start (cached after the first scan), reads the
   exact byte range, decodes it to `ERSwapper_<Item>.png` on your Desktop, previews it, and opens it
   in your default image editor.
3. Edit the PNG in any image editor. Keep the dimensions the same — if you don't, the app offers to
   resize a temporary copy.
4. **Apply Edited Texture** — re-encodes, strips the DDS header, verifies the payload is *exactly*
   the expected byte count, stores the original bytes for undo, shows a confirmation with every
   number involved, and writes.
5. **Swap History** — undo any single swap. It rewrites only that texture's bytes, so other swaps in
   the same bundle are left alone.
6. **Reset All Swaps** — undoes every swap that is still applied, in one go. The history is kept
   afterwards, so it can be run again any time. If one fails (locked, missing), the rest still
   restore and the failures are listed.

Rust must be fully closed for steps 4–6. The app checks for the running process and re-checks
immediately before the write, in case the game was launched while the confirmation dialog was open.

---

## Adding items

The gallery is just `Config/presets.json`. Add an entry and the item appears on the next launch —
no rebuild, no tooling.

Every field comes straight out of a UABEA dump, so the work is reading the dump and copying numbers
across:

1. Open the bundle in [UABEA](https://github.com/nesrak1/UABEA), find the texture, **Export Dump**.
2. Open the dump and read `m_Name`, `m_Width`, `m_Height`, `m_MipCount`, `m_TextureFormat`, and the
   `m_StreamData` block's `offset` and `size`.
3. Add an object to `Config/presets.json`:

```json
{
  "DisplayName": "AK47",
  "Category": "Weapons",
  "TextureObjectName": "ak47_combined_bc",
  "BundleRelativePath": "shared\\textures.1.bundle",
  "ResSSignatureSourcePath": "CAB-154587b10caab4209fca197fec8809e6.resS.sig",
  "StreamDataOffset": 3677402608,
  "StreamDataSize": 2796216,
  "Width": 2048,
  "Height": 2048,
  "MipCount": 12,
  "DxgiFormat": "BC1_UNORM"
}
```

| Field | Where it comes from |
|---|---|
| `DisplayName` | Anything you like — this is the gallery label |
| `Category` | `Weapons`, `Tools`, `Medical`, `Clothing`, `Deployables`, `Resources`, `Other` |
| `TextureObjectName` | `m_Name` from the dump |
| `BundleRelativePath` | The bundle's path under `Bundles\`, with **escaped** backslashes |
| `ResSSignatureSourcePath` | The `.sig` in `Config/Signatures/` for that bundle — see `Config/bundles.json` for the mapping |
| `StreamDataOffset` | `m_StreamData.offset`, relative to the `.resS`, **not** the bundle |
| `StreamDataSize` | `m_StreamData.size` |
| `Width` / `Height` / `MipCount` | `m_Width`, `m_Height`, `m_MipCount` |
| `DxgiFormat` | `BC1_UNORM` for `m_TextureFormat` 10, `BC3_UNORM` for 12 |

Only `BC1_UNORM`, `BC2_UNORM` and `BC3_UNORM` can be written. Any other `m_TextureFormat` is listed
under **What cannot be swapped?** and cannot be added by any means.

Getting a number wrong does not corrupt anything. `StreamDataSize` has to match what the encoder
produces from those dimensions, or the write is refused before a single byte is touched — the same
check that makes every other swap safe.

---

## Safety rules the code enforces

These are hard stops, not warnings — textures sit back to back in the `.resS`, so a wrong-size write
would overwrite a neighbouring texture:

- The encoded payload must match `StreamDataSize` **exactly** or nothing is written.
- The write range must lie fully inside the bundle.
- The DDS produced by texconv must actually be the format the preset declared.
- The signature must match at exactly **one** offset — zero or multiple matches abort the operation.
- The original bytes are read and stored **before** the write, so every swap is undoable the moment
  it happens.
- Rust must be closed. The check runs again immediately before the write, in case the game was
  launched while the confirmation dialog was open.

### Undo is per swap, not per bundle

A swap writes exactly `StreamDataSize` bytes at one offset and nothing else, so the only thing
needed to undo it is those bytes. Every swap saves its own original region to
`%LocalAppData%\ERSwapper\History\` along with before/after thumbnails, and **Swap History** lists
them:

```
Item      When              Bundle              Status
AK47      2026-08-14 06:29  shared / textures.1 Applied
Pickaxe   2026-08-13 21:04  shared / textures.1 No longer applied — the original texture is back
Rock      2026-08-12 18:40  shared / textures.1 Replaced by a newer swap of the same texture
```

**Put Original Back** rewrites only that texture's bytes, so other swaps in the same bundle are
untouched — something a whole-bundle backup cannot do, since one bundle holds thousands of
textures. It refuses unless the bytes currently in the game are the ones that swap wrote, so it can
never clobber a newer swap or fight with Steam.

Backups made by older versions, sitting next to the bundle as `<bundle>.BACKUP`, are still found
and still used. If one exists it is preferred, so nothing you already have stops working.

---

## Layout

```
ERSwapper.sln
├── ERSwapper.Core/                 shared engine — no UI decisions live here
│   ├── DdsHeaderBuilder.cs         build/strip 128-byte DX9 DDS headers
│   ├── ResSOffsetLocator.cs        streaming signature search + portable offset cache
│   ├── TexconvWrapper.cs           texconv.exe process wrapper
│   ├── BundlePatcher.cs            backup, read, write at offset, restore
│   ├── ProcessLockChecker.cs       detect a running game
│   ├── BundleLocator.cs            resolve bundle paths under the install
│   ├── RustInstallLocator.cs       Steam auto-detection
│   ├── UabeaDumpParser.cs          read UABEA text/JSON texture dumps
│   ├── DumpLookup.cs               match a dropped file to its UABEA dump
│   ├── SignatureStore.cs           imported + shipped signatures
│   ├── UnityBundleReader.cs        read a bundle's directory to locate its .resS
│   ├── Lz4Block.cs                 LZ4 decoder for the bundle directory
│   ├── CabIdentity.cs              recover a bundle's CAB id from any name
│   ├── BundleIndex.cs              map CAB id to the bundle containing it
│   ├── ImageOrientation.cs         flip between Unity's bottom-up rows and PNG
│   ├── ShippedAssets.cs            seed user data from what ships with the app
│   ├── UnsupportedRegistry.cs      record dumps this pipeline cannot write
│   ├── ItemSearch.cs               gallery search filter
│   ├── ThumbnailCache.cs           cached 128px item thumbnails
│   ├── ThumbnailGenerator.cs       read-only thumbnail production
│   ├── Theme.cs                    dark palette, title bar, native styling
│   ├── CardPanel.cs                themed titled surface, replaces GroupBox
│   └── AppSettings.cs / AppPaths.cs / PresetStore.cs / ItemPreset.cs
│
├── ERSwapper/                      shipped end-user app
│   ├── LoadingForm.cs              splash that prepares offsets and previews
│   ├── StartupLoader.cs            the work the splash runs
│   ├── MainForm.cs                 category rail + gallery + three actions
│   ├── texconv.exe                 shipped so nothing has to be downloaded
│   └── Config/                     presets.json, Signatures, Thumbnails, offset_cache.json
```

### Where files live at runtime

Files are split by one rule: **if the download can produce it, it lives next to the exe. If only
your machine can produce it, it lives in AppData.**

**Next to `ERSwapper.exe` — replaceable**

```
ERSwapper.exe
texconv.exe                  downloads itself if missing
Config/
├── presets.json             the item catalogue — edit this to add items
├── bundles.json             which .sig belongs to which bundle
├── unsupported.json         textures that cannot be written
├── release.json             version + what the installer needs to handle it
├── offset_cache.json        starting offsets, saves the first scan
├── Signatures/              4 KB fingerprints, one per bundle
└── Thumbnails/              pre-built gallery previews
```

`Config` is disposable. An update **mirrors** this folder rather than merging it, so a file removed
in a new version actually disappears instead of lingering. Nothing here is written at runtime, which
is what makes replacing it wholesale safe.

That is also why `presets.json` lives here rather than in AppData: it ships with the download, so
updates deliver new items to everyone, and anyone can open it and add their own.

**In `%LocalAppData%\ERSwapper\` — yours, never touched by updates**

```
settings.json                Rust folder, Config folder, preferences
layout.json                  which folder layout this data was written under
offset_cache.json            offsets learned on this machine
unsupported.json             textures this machine found it cannot write
History/                     swap log, original bytes for undo, before/after previews
Thumbnails/                  previews built here rather than shipped
Backups/                     whole-bundle copies, only if you turn them on
```

None of these folders exist until something is actually written, so a fresh install leaves just
`settings.json` and `layout.json`.

Where both sides have the same filename — `offset_cache.json`, `unsupported.json` — the shipped copy
is a read-only starting point and the AppData copy is what this machine learned. They are merged on
read; the shipped one never overwrites yours.

Deleting the whole AppData folder is safe **unless swaps are currently applied** — the original bytes
that undo them live in `History/`. With nothing applied you lose only your settings, which are
re-detected on the next launch.

The folder is named after the executable, so two builds side by side never read each other's
settings or history.

---

## License

[MIT](LICENSE). Use it, fork it, ship your own build — just keep the copyright notice.

`texconv.exe` is bundled from [Microsoft's DirectXTex](https://github.com/microsoft/DirectXTex),
also MIT licensed. If it is ever missing, ER Swapper downloads it on first run rather than asking
you to go and find it.
