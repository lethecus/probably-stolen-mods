# Compact Cassettes

Shrinks every music cassette from its 2x2 grid footprint down to a single 1x1 cell. Applies to newly created tapes and to tapes already stored in a save, so more cassettes fit in the player, in containers and in shop storage. The tape keeps its grid position; its sprite is drawn at one cell.

- Mod ID: `cassette_tape_size`
- Version: 1.0
- Author: Local
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\CassetteTapeSize`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build CassetteTapeSize.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.
