# Customer Counter

A small LCD panel next to the shop clock that shows how many customers are still waiting in line today. Installed automatically.

- Mod ID: `customer_counter`
- Version: 1.0
- Author: Local
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\CustomerCounter`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build CustomerCounter.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.
