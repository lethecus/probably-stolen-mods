# Blank Card Printer

Adds a Card Printer machine that prints vanilla blank keycards. Find it in toolTable scavenging or buy it from Dr. Jackson. Uninstall note: disabling the mod while you own a printer may remove it from your save.

- Mod ID: `blank_card_printer`
- Version: 1.0
- Author: Lethecus
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\BlankCardPrinter`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build BlankCardPrinter.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.