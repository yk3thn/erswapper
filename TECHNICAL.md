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

### 3. Bundle signatures — the one manual step

**This cannot be derived automatically and must be done once per bundle.**

The app locates a bundle's `.resS` blob by searching the bundle for a *signature*: the first 4 KB of
a known-clean `.resS`. To produce one:

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
2. **Extract Texture** — finds the `.resS` start (cached after the first scan), reads the exact byte
   range, decodes it to `ERSwapper_<Item>.png` on your Desktop, previews it, and opens it in your
   default image editor.
3. Edit the PNG in any image editor. Keep the dimensions the same — if you don't, the app offers to
   resize a temporary copy.
4. **Import Edited Texture** — re-encodes, strips the DDS header, verifies the payload is *exactly*
   the expected byte count, backs up the bundle, shows a confirmation with every number involved,
   and writes.
5. **Restore This Bundle** — copies the bundle's backup back over the live bundle.
6. **Reset All Swaps** — reverts *every* bundle that has a backup, undoing all swaps at once.
   Backups are found by scanning both locations, so a swap stays revertible even if you later
   edited or deleted its preset. Backups are kept afterwards, so it can be run again any time. If one
   bundle fails (locked, missing), the rest still restore and the failures are listed.

Rust must be fully closed for steps 4–6. The app checks for the running process and re-checks
immediately before the write, in case the game was launched while the confirmation dialog was open.

---

## Adding items

use uabea to find the dump and edit the presets.json to include that new item and it will reflect

---

## Safety rules the code enforces

These are hard stops, not warnings — textures sit back to back in the `.resS`, so a wrong-size write
would overwrite a neighbouring texture:

- The encoded payload must match `StreamDataSize` **exactly** or nothing is written.
- The write range must lie fully inside the bundle.
- The DDS produced by texconv must actually be the format the preset declared.
- The signature must match at exactly **one** offset — zero or multiple matches abort the operation.
- The bundle is backed up before its first write, and an existing backup is never overwritten.
- Backups are written to a `.partial` file and moved into place, so an interrupted copy can't leave
  a truncated file that looks like a valid backup.

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

The app keeps its own data under `%LocalAppData%\ERSwapper`

### Where files live at runtime

fix this

---

## License

[MIT](LICENSE). Use it, fork it, ship your own build — just keep the copyright notice.

`texconv.exe` is bundled from [Microsoft's DirectXTex](https://github.com/microsoft/DirectXTex),
also MIT licensed. If it is ever missing, ER Swapper downloads it on first run rather than asking
you to go and find it.
