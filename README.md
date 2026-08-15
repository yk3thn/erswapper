# ERSwapper
ERSwapper is an easy way to edit the byte data of texture assets in the video game Rust.
Open source, ERSwapper uses the Unity Asset Bundle System to locate, preview, edit, and reimport texture data for in-game dependencies.
![ERSwapper](https://github.com/yk3thn/erswapper/blob/main/ERSwapper_banner.png "ERSwapper Banner")

## Prerequisites
1. Windows 10/11
2. Steam / Rust
3. [Python 3.10.5](https://www.python.org/ftp/python/3.10.5/python-3.10.5-amd64.exe)
4. [DotNET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.30-windows-x64-installer)
5. ERSwapper

## Safety
```As a video game player and computer user, I've personally been a victim to viruses. I don't expect you to blindly trust me. That is why this entire project is open source, and I have no intention of hiding any of my findings. I not only allow but ENCOURAGE you to look through the source code and possibly compile it yourself. Add a feature or two and send me a message. This entire project comes from a passion for game modding, not cheating. This program does not require administrator privileges, but does tamper with files, create files, and delete files. The scope of the affect is limited to only what is necessary (or in some cases, efficient) to completing the task at hand. If you use this program, you are at the mercy of Facepunch, Rust, and Steam. I don't work with any of these companies (though i would like to), nor am I associated with or represent them.```

## Limitations
I am only 1 person so I can only launch Rust over and over again so many times, but here are the limits i've found:

1. Editing more than 2 bundles at once is unreliable
2. Editing more than 3 textures in ANY file is unreliable
3. Editing Icons that are 256x256 are unreliable
4. Editing meshes, materials, audio, or other data types are unsupported
5. Editing certain image formats are unsupported (See the "What cannot be swapped?" page in the swapper.)
6. Editing walls to be transparent is unreliable
7. Keeping backup files inside the Bundle folder does not affect the process
8. Adding junk data in the middle of a bundle seems to not affect the process
9. Adding extra data to the end of a bundle seems to not affect the process
10. Modded servers stream their assets, but im assuming there is a way to force a fallback on the assets on your disk
11. Editing AOs or Masks is unsupported for this program but I don't see why you couldn't do it manually
12. I've swapped at least 25 textures over the last 2 days and havent been banned or suspended (Subject to change)
13. Rust detects UABEA open
14. Rust does not detect ERSwapper open
