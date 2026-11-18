# Cassette Randomize

Modifies the tape player with a 90-row scrollable cassette deck (If using my other mod that turns all tapes into single cells 540 tapes fit within, 7 rows visible) and a hi-fi equalizer panel: spectrum and oscilloscope, seek slider with track clock, volume, bass, treble, balance, mono, three savable presets and a collapsible storage drawer. The Randomize button starts a random playable cassette and keeps shuffling at the end of every track until it is stopped.

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.

https://github.com/lethecus/probably-stolen-mods
The source for all my mods if you wish to confirm nothing shady going on.

- Mod ID: `cassette_randomize`
- Version: 3.4
- Author: Lethecus
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\CassetteRandomize`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build CassetteRandomize.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.
