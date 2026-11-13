# AutoSorter Plus

Rule-based storage sorting with boolean rules. Each container and each machine tab gets its own include/exclude expression supporting AND, OR, NOT, parentheses, tag/id/name wildcards and source-mod matching, so vanilla seeds and modded seeds can live on different shelves. Runs on triggers only (sort, overview and a global toggle hotkey) and stores its rules with the save. Includes English localization plus a localization folder for other languages.

- Mod ID: `auto_sorter_plus`
- Version: 1.0
- Author: Local
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\AutoSorterPlus`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build AutoSorterPlus.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.