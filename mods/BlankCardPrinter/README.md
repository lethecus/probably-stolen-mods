# 



- Mod ID: ``
- Version: 
- Author: 
- Game version: 

## Install

Copy this folder into `<game>\Mods\BlankCardPrinter`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build BlankCardPrinter.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.