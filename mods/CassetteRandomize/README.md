# Cassette Randomize

Replaces the tape player with a 50-row scrollable cassette deck (300 single-cell tapes, 7 rows visible) and a hi-fi equalizer panel: spectrum and oscilloscope, seek slider with track clock, volume, bass, treble, balance, mono, three savable presets and a collapsible storage drawer. The Randomize button starts a random playable cassette and keeps shuffling at the end of every track until Stop is pressed.

- Mod ID: `cassette_randomize`
- Version: 3.0
- Author: Local
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\CassetteRandomize`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build CassetteRandomize.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.