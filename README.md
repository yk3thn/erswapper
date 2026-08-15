# ERSwapper

ERSwapper is an easy way to edit the byte data of texture assets in the video game Rust.
Open source, ERSwapper uses the Unity Asset Bundle System to locate, preview, edit, and reimport texture data for in-game dependencies.

![ERSwapper](https://github.com/yk3thn/erswapper/blob/main/ERSwapper_banner.png "ERSwapper Banner")

## Prerequisites

1. Windows 10/11
2. Steam / Rust
3. [DotNET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.30-windows-x64-installer)
4. ERSwapper — [download the latest release](https://github.com/yk3thn/erswapper/releases/latest)

That's it. Rust is found automatically, and `texconv.exe` (the texture converter) either ships in the
zip or downloads itself on first run. There is nothing to configure before you start.

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

**Offline / single-player use only.** Do not join servers with anti-cheat enabled while your game
files are modified. Rust's asset integrity checks (EAC) are designed to detect this and it may result
in a ban. Every swap can be undone from **Swap History**, and Steam →
*Verify Integrity of Game Files* always restores the originals.

## Limitations

I am only 1 person so I can only launch Rust over and over again so many times, but here are the
limits i've found:

1. Editing more than 2 bundles at once is unreliable
2. Editing more than 3 textures in ANY file is unreliable
3. Editing Icons that are 256x256 are unreliable
4. Editing meshes, materials, audio, or other data types are unsupported
5. Editing certain image formats are unsupported (See the "What cannot be swapped?" page in the swapper.)

## Using it

1. Pick an item from the gallery.
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

**[TECHNICAL.md](TECHNICAL.md)** covers how it actually works: how a texture is located inside a
bundle, the offset maths, the safety rules the code enforces, and where files live at runtime.

## License

[MIT](LICENSE). Use it, fork it, ship your own build — just keep the copyright notice.

`texconv.exe` is bundled from [Microsoft's DirectXTex](https://github.com/microsoft/DirectXTex), also
MIT licensed.
