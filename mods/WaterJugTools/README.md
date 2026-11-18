# Water Jug Tools

Adds an Empty action to the right-click menu for owned liquid containers, and makes the shop tap fill any container to its full capacity - including when other mods change how much the tap fills at once.

- Mod ID: `water_jug_tools`
- Version: 1.1
- Author: Lethecus
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\WaterJugTools`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build WaterJugTools.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.
