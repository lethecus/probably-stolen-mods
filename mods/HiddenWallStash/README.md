# Hidden Wall Stash

A concealed 15x13 smuggler storage with ten tabs, installed at the start of every game. Click the metal grate on the wall above the shop radio to open it. Uninstall note: empty the stash before disabling this mod, or its contents are lost.

- Mod ID: `hidden_wall_stash`
- Version: 1.0
- Author: YourName
- Game version: 100-REV13

## Install

Copy this folder into `<game>\Mods\HiddenWallStash`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build HiddenWallStash.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.
