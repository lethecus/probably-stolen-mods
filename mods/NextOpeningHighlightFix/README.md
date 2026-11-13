# Next Opening Highlight Fix

Stops the repeating "Could not refresh item highlights" NullReferenceException from Next Opening: Suppliers & Odin. Logs the full stack once for diagnosis. Requires Next Opening: Suppliers & Odin.

- Mod ID: `next_opening_highlight_fix`
- Version: 1.0
- Author: Local
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\NextOpeningHighlightFix`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build NextOpeningHighlightFix.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.
