# ERSwapper

ERSwapper is an easy way to edit the byte data of texture assets in the video game Rust.
Open source, ERSwapper uses the Unity Asset Bundle System to locate, preview, edit, and reimport texture data for in-game dependencies.

![ERSwapper](https://github.com/yk3thn/erswapper/blob/main/ERSwapper_banner.png "ERSwapper Banner")

View the Youtube tutorial below:

[![ERSwapper YouTube Video](https://img.youtube.com/vi/rpI-dewq4es/0.jpg)](https://www.youtube.com/watch?v=rpI-dewq4es)

## Prerequisites

1. Windows 10/11
2. Steam / Rust
3. [DotNET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.30-windows-x64-installer)
4. ERSwapper — [download the latest release](https://github.com/yk3thn/erswapper/releases/latest)

### Manual Swaps

In case you do not want to run this program, you can perform a manual byte data overwrite.
For manual swaps, you are going to need:
1. [texconv.exe](https://github.com/yk3thn/erswapper/raw/refs/heads/main/texconv.exe)
2. [png2raw.py](https://github.com/yk3thn/erswapper/raw/refs/heads/main/Tools/png2raw.py)
3. [png2raw.bat](https://github.com/yk3thn/erswapper/raw/refs/heads/main/Tools/png2raw.bat)
4. [HxD](https://mh-nexus.de/downloads/HxDSetup.zip)
5. [The tutorial at the bottom of this README](https://github.com/yk3thn/erswapper/tree/main#how-to-perform-a-manual-swap)

## Safety

> As a video game player and computer user, I've personally been a victim to viruses. I don't expect
> you to blindly trust me. That is why this entire project is open source, and I have no intention of
> hiding any of my findings. I not only allow but ENCOURAGE you to look through the source code and
> possibly compile it yourself. Add a feature or two and send me a message. This entire project comes
> from a passion for game modding, not cheating. This program does not require administrator
> privileges, but does tamper with files, create files, and delete files. The scope of the affect is
> limited to only what is necessary (or in some cases, efficient) to completing the task at hand. If
> you use this program, you are at the mercy of Facepunch, Rust, and Steam. I don't work with any of
> these companies (though i would like to), nor am I associated with or represent them.

*Verify Integrity of Game Files* always restores the originals.

<img width="420" height="300" alt="image" src="https://github.com/user-attachments/assets/7b5bfa90-15af-4400-a586-6cefb1f5eb31" />

## Using it

1. Pick an item from the gallery and click on it.
2. **Extract & Open in Editor** — the current texture opens as a PNG in your image editor.
3. Edit it, keeping the same dimensions, and save.
4. **Apply Edited Texture** — it is converted back and written into the bundle.

Rust must be closed while applying, and the app will tell you if it isn't.

| Button | What it does |
|---|---|
| **Swap History** | Every swap you've made, with before/after previews. Undo any single one without touching the others. |
| **Reset All Swaps** | Puts everything back at once. |
| **What cannot be swapped?** | The textures Rust stores in a form this tool can't rewrite. |
| **Rebuild Previews** | Re-reads every preview from the game, if one looks wrong. |
| **Settings** | Rust folder, Config folder, manual update check, clear history. |

view **[TECHNICAL.md/Adding Items](https://github.com/yk3thn/erswapper/blob/main/TECHNICAL.md#adding-items)** to learn how to add select supported items manually.

## What I've found

- Editing walls to be transparent is unreliable
- Keeping backup files inside the Bundle folder does not affect the process
- Adding junk data in the middle of a bundle seems to not affect the process
- Adding extra data to the end of a bundle seems to not affect the process
- Modded servers stream their assets, but im assuming there is a way to force a fallback on the assets on your disk
- Editing AOs or Masks is unsupported for this program but I don't see why you couldn't do it manually
- I've swapped at least 25 textures over the last 2 days and havent been banned or suspended (Subject to change)
- Rust detects UABEA and HxD open
- Rust does not detect ERSwapper open
- This program is built such that if Rust changes the format of the assets, it can be rebuilt to support the new system
- EAC does not check the hash or file size of the bundle files reliably. Sometimes it does, sometimes it doesn't
- I can't test all the assets available so if you find one that doesnt work please let me know

## Limitations

I am only 1 person so I can only launch Rust over and over again so many times, but here are the
limits i've found:

1. Editing more than 2 bundles at once is unreliable
2. Editing more than 3 textures in ANY file is unreliable
3. Editing Icons that are 256x256 are unreliable
4. Editing meshes, materials, audio, or other data types are unsupported
5. Editing certain image formats are unsupported (See the "What cannot be swapped?" page in the swapper.)

## Building from source

```bash
dotnet build ERSwapper.sln
```

Needs the .NET 8 SDK. `ERSwapper.sln` contains the app and the engine:

| Project | What it is |
|---|---|
| **ERSwapper** | The app — gallery, extract/apply, history, settings. |
| **ERSwapper.Core** | The engine — bundle parsing, offset maths, DDS handling, patching, swap history. |

The catalogue of swappable items is curated with a separate authoring tool that isn't published here.
You don't need it to build, run or modify ERSwapper — `Config/presets.json` is plain JSON you can
extend by hand.

view **[TECHNICAL.md](TECHNICAL.md)** for more information.

#### How To Perform a Manual Swap:

1. Put all 3 files in the same folder as your edited texture
2. get [UABEA](https://github.com/nesrak1/UABEA)
4. drag and drop a bundle into UABE and then click on the drop down and select the resS
5. click "Export" and then click the drop down and reselect the regular CAB then click "Info"
6. click on the asset you want to edit and then click "Export Dump" and Plugins/Export Texture
7. Close UABEA and edit the png you exported
8. Replace the expect number inside png2raw.bat with the size of the asset located in the dump you exported
9. rename the png file to "texture.png" and then open up png2raw.py
10. if it says "Matches the target texture. Safe to overwrite in place." then you can proceed
11. take the size of the bundle (ex. 7027660544) and subtract the size of the resS (ex. 7025472480) to get the resS offset (ex. 2188064)
12. add the resS offset to the asset offset found in the dump (ex. 4341665408) to get your asset offset (ex. 4343853472)
13. do CTRL+E to search for the asset offset using the "dec" selection and then for the "Length" put the size of the asset found in the dump
14. everything selected is the byte data of the asset
15. use CTRL+C to copy the data from the texture.bin that was created from texture.png
16. use CTRL+B (NOT CTRL+V) to overwrite the selected data in the bundle
17. save the bundle with CTRL+S and then launch Rust

## License

[MIT](LICENSE).

`texconv.exe` is bundled from [Microsoft's DirectXTex](https://github.com/microsoft/DirectXTex), also
MIT licensed.
