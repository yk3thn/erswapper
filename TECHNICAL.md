# ER Swapper

Windows Forms (.NET 8) tools for swapping texture assets inside Rust's Unity asset bundles.

## What's in this repo

| Project | What it is |
|---|---|
| **ERSwapper** | The app people download. Extract → edit → apply, with everything pre-configured. |
| **ERSwapper.Core** | The engine: bundle parsing, offset maths, DDS handling, patching, swap history. |
| **ERSwapper.Tests** | The test suite covering all of the above. |

The catalogue of swappable items is curated with a separate authoring tool that isn't published
here. It reads Rust's texture dumps, previews candidates, and writes the `Config` folder that ships
with the app. Nothing reaches the catalogue without being deliberately added, which is why it stays
a useful list rather than every texture in the game.

That means **you don't need it to build, run, or modify ER Swapper** — the app and the engine are
complete on their own. You would only need something like it to add brand new items to the
catalogue, and `Config/presets.json` is plain JSON you can extend by hand
([format below](#4-edit-presetsjson-directly)).

---

> **Offline / single-player use only.** Do not join servers with anti-cheat enabled while game
> files are modified. Rust's asset integrity checks (EAC) are designed to detect this, and it may
> result in a ban or other action against your account. Every bundle is backed up before its first
> write, and you can revert at any time with **Restore Backup** or via
> Steam → *Verify Integrity of Game Files*.

---

## What it actually does

Rust's textures live inside large `.bundle` files (Unity `UnityFS` containers). Inside each bundle
is a `.resS` blob — the raw block-compressed pixel data for every streamed texture in that bundle,
concatenated back to back with no padding.

Each texture object records:

- `m_StreamData.offset` — a byte offset **relative to the start of the `.resS` blob**
- `m_StreamData.size` / `m_CompleteImageSize` — its byte length
- width, height, mip count, texture format

To read or write one texture we need its **absolute** offset inside the physical `.bundle`:

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

Download `texconv.exe` and either drop it next to `ERSwapper.exe`, put it in
`<Desktop>\rust_mod\`, or put it on your `PATH` — all three are auto-detected. Otherwise point
**Settings → texconv.exe** at it.

Auto-detection re-runs on every launch for any path that is blank or no longer resolves, so a tool
installed after first run is picked up rather than staying unset. A path that still works is never
overwritten.

### 2. Rust install folder

Auto-detected from Steam's registry entry and `libraryfolders.vdf`. Override in **Settings** if the
guess is wrong — it should be the folder containing `RustClient.exe`.

### 3. Bundle signatures — the one manual step

**This cannot be derived automatically and must be done once per bundle.**

The app locates a bundle's `.resS` blob by searching the bundle for a *signature*: the first 4 KB of
a known-clean `.resS`. To produce one:

1. Open the bundle in [UABEA](https://github.com/nesrak1/UABEA).
2. Export the `.resS` for that bundle (Info → the `.resS` / resource file).
3. In ER Swapper: **Settings → Add bundle signature…** and select the exported file.
   Only the first 4 KB is stored; the export itself can be deleted afterwards.

**The file name does not matter.** UABEA names `.resS` exports after the bundle's internal CAB id
(e.g. `CAB-154587b10caab4209fca197fec8809e6.resS`), which is opaque and changes between game
versions. A preset's `ResSSignatureSourcePath` is tried first as a file name, but if it doesn't
exist the app falls back to every `.sig` in the folder and keeps whichever one actually occurs in
the target bundle — a signature only matches the blob it was cut from, so this is self-validating.
All candidates are tested in a **single pass** over the bundle, so extra signatures cost nothing.

If two different signatures each match the same bundle, the app aborts rather than guess; remove
the ones belonging to other bundles via **Settings → Open data folder**.

Because the name is only a label, the app keeps it honest rather than letting it drift:

- Every form with a signature field has a **Browse…** button. It accepts an already-imported
  `.sig` *or* a raw `.resS` straight out of UABEA — picking a `.resS` imports it on the spot, so a
  new bundle can be set up without a detour through Settings.
- On startup, any preset naming a signature that doesn't exist is **repointed automatically** to
  the installed one, and the change is saved. This only happens when exactly one signature is
  installed; with several it would be a guess, so the presets are left alone.

Do this **before** the bundle is ever patched — a signature taken from an already-modified bundle
may not match the region it was cut from.

---

## The shipped app (ERSwapper)

A **loading screen** runs first and does everything slow up front — locating the texture data
inside the bundle and building item previews — so the gallery is complete the moment the main
window appears rather than filling in while the user is trying to use it. The only question it can
ever ask is where Rust is installed, and only if auto-detection fails.

The main window is a **category rail** (Weapons, Tools, Deployables…) beside a gallery of item
tiles. Pick a category, pick an item, then:

1. **Extract & Open in Editor** — writes the current texture to your Desktop as a PNG and opens it
2. Edit and save it, keeping the same dimensions
3. **Apply Edited Texture** — converts it back and patches it in, after backing up the game file

**Reset All Swaps** puts everything back. **What cannot be swapped?** lists the textures Rust stores
in a form this tool cannot rewrite — so users can check for themselves rather than asking for
something impossible. **Rebuild Previews** deletes the cached preview images and reads them from the
game again, for when a preview looks wrong or stale; it touches no game files and no swaps. That is
the entire interface — no paths, no offsets, no signatures, no dump import.

It ships self-contained: the item catalogue, the `.resS` signatures, pre-built thumbnails, the
offset cache and `texconv.exe` all live in the output folder.

## The item gallery (authoring tool)

> The rest of this section describes the authoring tool used to curate the catalogue. It is not in
> this repo — it is here so the shipped `Config` folder makes sense, and so anyone maintaining a
> fork knows how the data was produced.

The main window is a gallery of item tiles, each showing its current in-game texture, grouped by
category — pick the thing you want to change, then extract/edit/import it.

- **Search** filters as you type across item name, texture object name and category. Multiple terms
  narrow rather than widen: `ak weapons` finds the AK47 under Weapons, `ak tools` finds nothing.
- **The bundle dropdown** narrows to one source bundle — `shared\textures.1.bundle`,
  `shared\textures.2.bundle` and so on — with the item count beside each. The list is derived from
  the catalogue, so a bundle appears as soon as items from it are added and disappears when the
  last one goes. It composes with the search rather than replacing it, so you can filter to one
  bundle and then search within it.
- **Tiles are drawn by the app**, not the system, so the selected tile gets an accent outline
  instead of the light highlight box Windows would paint on a dark background.
- The preview, details and item buttons sit in a narrow right-hand rail, leaving the gallery the
  bulk of the window.

## Dark theme

The whole UI is dark, including the window title bar (via `DWMWA_USE_IMMERSIVE_DARK_MODE`) and the
ListView's scrollbars and group headers (via the `DarkMode_Explorer` visual style). Both are native
calls that fail harmlessly on older Windows builds, leaving those parts light rather than broken.

Two controls need special handling and get it: `ProgressBar` ignores its own colour properties
while visual styles are on, so styles are dropped for that one control; and `GroupBox` draws its
frame in an unoverridable system colour, so it is replaced throughout by a `CardPanel` that paints
its own border and title.

Message boxes are drawn by Windows and stay light — there is no supported way to theme them.

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

Items are grouped into categories (Weapons, Tools, Medical, …) in the list. There are three ways
to add them, in order of how much typing they involve.

### 1. Browse textures (best for finding things)

**Browse textures…** in the authoring tool opens every texture Rust ships, as a grid of actual
previews, so you can see what something is instead of exporting it from UABEA to find out.

- **Base colour only** hides the normal, AO, mask and gloss maps, which are never worth swapping.
  That alone cuts roughly 17,000 textures down to about 2,500.
- **Hide items already in the catalogue** and **hide unsupported formats** narrow it further.
- The bundle dropdown and the name filter compose with all of that, so `door` in
  `shared / textures.1` is a handful of tiles.
- Selecting a single tile shows its name, dimensions, format, byte size and bundle along the
  bottom.

A loading screen reads the dumps and builds the first few screenfuls before the window opens, so
there is something to look at immediately. After that, **only tiles on screen are built** — one
screenful of lookahead, four at a time, cancelled the moment you scroll away or change a filter.
The work is always bounded; the browser never grinds through thousands of textures you are not
looking at.

Previews decode a single small mip rather than the whole texture. Mip 4 of a 2048x2048 is already
128x128 and 8 KB, against 2.8 MB for the full chain, which makes each preview roughly ten times
cheaper. They are cached to disk, so a tile is only ever built once.

Select any number of tiles, pick a category and press **Queue selected**. The window stays open, so
you can queue a few weapons, switch the category, queue a couple of medical items, and keep going —
**each batch keeps the category it was queued under**. Queued tiles turn green and the running total
shows what is waiting:

```
Queued to add: 9   (Resources 4, Weapons 3, Medical 2)
```

**Done** asks once, listing what is about to be added, and only then does anything reach the
catalogue. **Clear queued** throws the whole queue away, and closing the window with something still
queued asks first rather than silently dropping it. Nothing is added without that final
confirmation, and items already in the catalogue are marked so they are not added twice.

Dumps are read head-and-tail rather than whole. Everything the browser needs sits in the first
few hundred bytes of a dump except `m_StreamData`, which sits in the last hundred — between them
is the `image data` array, which can be hundreds of megabytes and is never used. Skipping it turns
a 33 GB read into about 450 MB.

The result is cached per file in `texture_index.json`, keyed on size and modified time, so exports
added later cost only the files you added.

### 2. Drag and drop (fastest for a known item)

**Drop a PNG anywhere on the window.** The app takes the file's name, searches the dumps folder
for a matching UABEA dump, and opens the importer with it already parsed.

Matching handles both naming styles UABEA produces:

| Dropped file | Matched by |
|---|---|
| `ak47_combined_bc-CAB-154587b1…-4725939743.png` | exact stem → `….json` |
| `v_pickaxe_bc.png` | texture name → `v_pickaxe_bc-CAB-*.json` |
| `ERSwapper_Pickaxe.png` | our own export prefix is stripped, then as above |

Drop several files at once and they all land in one importer. Dropping a `.json` / `.txt` /
`.dump` directly skips the lookup. If a texture name appears more than once in the bundle, every
candidate is listed so you pick rather than the app guessing. Anything with no match is reported
with the folder that was searched.

The dumps folder defaults to `<Desktop>\rust_mod\dumps` and is configurable in **Settings**.

**The signature and bundle are worked out for you.** Unity names each bundle's container
`CAB-<32 hex>`, and that id appears in three places the drop already has: the dump's
`m_StreamData.path`, the dump's own file name, and the name of any texture exported alongside it.
From it the app derives:

- the signature name — `CAB-….resS.sig`
- **which bundle the texture actually lives in**, by reading every bundle's directory once
  (~125 ms for a whole Rust install) and matching the CAB id

So a batch spanning several bundles lands correctly instead of inheriting one path, which is what
otherwise produces items silently pointing at the wrong bundle. The import dialog shows what was
detected per row; the Bundle and Signature fields underneath are only fallbacks, used for textures
whose CAB id could not be recovered.

**Unsupported textures are recorded, not just refused.** If a dropped image resolves to a texture
this pipeline cannot write — a non-DXT format like BC7, or data embedded rather than streamed — the
dump's name is written to `unsupported.json` with status `unsupported` and the reason:

```json
[
  {
    "DumpFile": "SomeThing_nrm-CAB-154587b1….json",
    "TextureName": "SomeThing_nrm",
    "Status": "unsupported",
    "Reason": "unsupported m_TextureFormat 25 (only DXT1/DXT3/DXT5 can be written)",
    "TextureFormat": 25,
    "Width": 2048,
    "Height": 2048,
    "RecordedUtc": "2026-08-11T21:47:00.0000000Z"
  }
]
```

Re-dropping the same file refreshes its row rather than adding a duplicate. The file doubles as a
list of what supporting another format would unlock.

### 3. Import from UABEA dumps (button)

**Import items from UABEA dumps…** under the item list.

In UABEA, select each Texture2D and use **Export Dump** (text or JSON). Then import those files —
the offset, size, dimensions and texture format are read straight from the dump, so no numbers are
transcribed by hand, which is where wrong offsets come from. Multi-select is supported, so a batch
of items goes in at once.

The importer:

- reads both UABEA text dumps and JSON dumps
- **recomputes the mip count from the payload size** rather than trusting the dump
- greys out textures it can't write (non-DXT formats, or data embedded rather than streamed) with
  the reason shown in the Status column
- updates an existing item instead of duplicating it when the same texture is re-imported

You still supply the bundle path and category once for the batch, since the dump doesn't record
which bundle it came from.

### 3. Add item manually

**Add item…** opens an editor with all the fields, live consistency checking, and an
**Infer from size** button that solves for the mip count.

The important button is **Test read & preview**. It resolves the bundle, reads the exact byte
range and reports what's actually there — distinct byte values, percentage of zeros, and a
decoded preview if texconv is configured. **It never writes.** A wrong offset shows up as
"almost no variation — this looks like padding" instead of as a corrupted texture.

Test every new item before importing an edited image into it.

### Thumbnails

Each item shows a small thumbnail of its current in-game texture beside its name, so the list is
identifiable at a glance rather than a wall of names.

Thumbnails are generated in the background the first time an item appears and cached to disk, so
after the first pass the list loads instantly. Generation is read-only and is cancelled the moment
any foreground operation starts — it can never hold a bundle open while a patch is being written.

The cache key covers everything that determines the pixels (bundle, object, offset, size,
dimensions, format), so editing an item's numbers automatically invalidates its thumbnail instead
of leaving a stale picture next to changed values. Extracting or importing a texture refreshes the
thumbnail immediately from the PNG already on hand, with no extra bundle read. Thumbnails left
behind by removed or retuned items are pruned on startup, so the cache stays the size of the item
list.

Thumbnails need texconv. Without it the list simply shows names — never an error.

### 4. Edit presets.json directly

**Settings → Edit presets.json…** opens the file. Pull the values from UABEA's texture view:

```json
{
  "DisplayName": "Rock",
  "Category": "Tools",
  "TextureObjectName": "v_rock_bc",
  "BundleRelativePath": "shared\\textures.1.bundle",
  "ResSSignatureSourcePath": "Config\\Signatures\\textures.1.resS.sig",
  "StreamDataOffset": 1234567890,
  "StreamDataSize": 349544,
  "Width": 1024,
  "Height": 512,
  "MipCount": 11,
  "DxgiFormat": "BC1_UNORM"
}
```

Presets in an already-known bundle reuse its signature — no new `.sig` needed.

`BundleRelativePath` is resolved against the install root and then against `Bundles\`,
`RustClient_Data\Bundles\` and similar, since the layout has moved between versions. Note that Rust
ships **two** files named `textures.1.bundle` — a ~6.5 GB one under `Bundles\shared\` and a much
smaller one under `Bundles\textures\`. The seeded presets target the `shared` one. If a relative
path can't be resolved and multiple same-named files exist, the app aborts rather than guess.

A healthy preset's `StreamDataSize` equals the full BC1 mip-chain size implied by its
width/height/mip count. The app computes this and warns before encoding if they disagree, which
almost always means one of the values was misread.

**Supported formats:** `BC1_UNORM` (DXT1), `BC2_UNORM` (DXT3), `BC3_UNORM` (DXT5). BC4/BC5/BC6H/BC7
need a DXT10 header and are rejected rather than written incorrectly.

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

The status is worked out from the bytes themselves, not from timestamps: a swap you apply to a
bundle changes that file's modified time, which would otherwise make every earlier swap in the same
bundle look undone.

This is also what keeps the footprint honest. One AK47 swap stores **2.8 MB**, where copying its
bundle would take **6.7 GB**. When a swap is no longer applied — you reverted it, or Steam's *Verify
Integrity of Game Files* put the original back without telling anyone — the stored bytes are
released and the entry stays in the list, greyed out, so the history survives but the disk does not.

### Full bundle backups

Copying whole bundles is **off by default**, behind a checkbox in Swap History. Turning it on backs
up each bundle before its first swap, which is a real safety net but an expensive one: all 19 mapped
bundles come to **47.6 GB**.

**Full bundle backups…** in the same window lists any that exist, says whether each is still doing a
job, and deletes the ones that are not. A backup is only ever offered for deletion when the history
shows no swap still applied in that bundle; one with no history at all is kept and labelled, because
it may be the only way back.

### Where backups live

When they are enabled, backups are kept in the app's own folder, **not** beside the bundle they came
from:

```
%LocalAppData%\ERSwapper\Backups\
    textures.1.bundle.3f9a2c11.BACKUP
    textures.1.bundle.3f9a2c11.BACKUP.origin
```

Keeping them out of the game folder means Steam never sees stray files next to the bundles, and
nothing large is left behind inside the install.

The name carries a fingerprint of the bundle's full path, so `shared\textures.4.bundle` and
`textures\textures.4.bundle` get separate backups instead of overwriting each other. The `.origin`
file next to each backup records which bundle it came from, which is how a restore finds its way
home. The filename is lower-cased before fingerprinting so the same bundle can never end up with
two backups — a second one would be taken from an already-swapped file and would restore the
wrong bytes.

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
│
└── ERSwapper.Tests/                396 tests over the DDS math, signature search, patcher, dump
                                    parsing and lookup, item search, unsupported registry, path
                                    resolution, backups, swap history and the update contract
```

Each app keeps its own data under `%LocalAppData%\<app name>`, keyed on the executable name, so two
builds on one machine never read each other's settings or history.

### Where files live at runtime

`Config\presets.json` ships next to the executable as **seed data** and is copied on first run to:

```
%LocalAppData%\ERSwapper\
├── settings.json
├── presets.json          your editable copy
├── offset_cache.json     cached .resS offsets, keyed by path + size + mtime
├── unsupported.json      dumps that cannot be written, with reasons
├── Signatures\*.sig
└── Thumbnails\*.png      128px item thumbnails, keyed by texture identity
```

This differs slightly from the original spec, which put all config beside the executable. Writing to
the install directory fails whenever the app lives somewhere unwritable (Program Files), and it
would also mean a reinstall wipes hand-edited presets.

---

## Building

```bash
dotnet build ERSwapper.sln
```

```bash
dotnet test ERSwapper.sln
```

Open `ERSwapper.sln` in Visual Studio 2022 to use the WinForms designer; both `.Designer.cs` files
are written in the standard designer format and round-trip normally.

## License

[MIT](LICENSE). Use it, fork it, ship your own build — just keep the copyright notice.

`texconv.exe` is bundled from [Microsoft's DirectXTex](https://github.com/microsoft/DirectXTex),
also MIT licensed. If it is ever missing, ER Swapper downloads it on first run rather than asking
you to go and find it.
