# Building the mods

## Requirements

- Windows
- .NET SDK 6.0 or newer (`dotnet --list-sdks`)
- Probably Stolen installed through Steam

The mods target `netstandard2.1` and reference these game assemblies:

- `Assembly-CSharp.dll`
- `0Harmony.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.UI.dll`
- `Unity.TextMeshPro.dll` (only the mods that use it)

They all live in `<game>\Probably Stolen_Data\Managed\`.

## Telling the build where the game is

The projects use the `GameDir` property, defined in `Directory.Build.props`:

```xml
<GameDir Condition="'$(GameDir)' == ''">C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen</GameDir>
```

Either edit that default, set an environment variable, or pass it on the command line:

```powershell
dotnet build mods\SaveAnywhere\SaveAnywhere.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

## Build one mod

```powershell
dotnet build mods\<ModName>\<ModName>.csproj -c Release
```

The DLL appears in `mods\<ModName>\bin\Release\netstandard2.1\`.

## Build everything

```powershell
.\build-all.ps1
```

Optional arguments:

```powershell
.\build-all.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
.\build-all.ps1 -Deploy                      # also copy manifest + DLL + assets into <game>\Mods
.\build-all.ps1 -Deploy -ModsDir "D:\somewhere\Mods"
```

`-Deploy` copies, for each mod:

- `manifest.xml`
- every DLL listed in `<Assemblies>`
- asset folders next to the source (`Textures`, `Localization`, `Sounds`, ...)

It never deletes anything in the Mods folder.

## Installing manually

Create `<game>\Mods\<ModName>\`, put the built DLL and `manifest.xml` there (plus any asset
folders), then activate the mod in the game's Mods panel and restart.

## Common problems

| Symptom | Fix |
| --- | --- |
| `Assembly-CSharp.dll` not found | `GameDir` points at the wrong folder; it must be the folder containing `Probably Stolen_Data`. |
| Build succeeds but the mod does not load | Check that the manifest `<Assemblies>` entry matches the DLL file name exactly. |
| Game shows the mod as `trust=Updated` | Normal after rebuilding: the loader notices the DLL changed. |
| Textures missing in game | Copy the mod's `Textures` folder next to the DLL. |
