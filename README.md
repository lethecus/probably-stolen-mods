# Probably Stolen - mod sources

Source code for the mods we build for **Probably Stolen** (Steam App ID 4348910).
Everything here is the code that produces the DLLs installed in the game's `Mods` folder.

Licensed under **GPL-3.0** (see `LICENSE`).

| Mod | ID | Version | What it does |
| --- | --- | --- | --- |
| [SaveAnywhere](mods/SaveAnywhere) | `save_anywhere` | 1.1 | Save button in the escape menu; mid-day saves keep the number of customers left |
| [AutoSorterPlus](mods/AutoSorterPlus) | `auto_sorter_plus` | 1.0 | Rule-based container/machine sorting with `tag:`, `id:`, `name:`, `mod:`, `label:` rules |
| [CassetteRandomize](mods/CassetteRandomize) | `cassette_randomize` | 3.0 | 50-row cassette deck, equalizer and shuffle |
| [CassetteTapeSize](mods/CassetteTapeSize) | `cassette_tape_size` | 1.0 | Shrinks cassettes from 2x2 to 1x1 |
| [ContaminantConverter](mods/ContaminantConverter) | `contaminant_converter` | 1.2 | Powered chemistry machine that converts one liquid into another |
| [WaterJugTools](mods/WaterJugTools) | `water_jug_tools` | 1.0 | Right-click "Empty" for liquid containers; tap fills any container |
| [CustomerCounter](mods/CustomerCounter) | `customer_counter` | 1.0 | LCD panel showing how many customers are still waiting today |
| [HiddenWallStash](mods/HiddenWallStash) | `hidden_wall_stash` | 1.0 | Concealed 15x13 smuggler storage behind the shop grate |
| [BlankCardPrinter](mods/BlankCardPrinter) | `blank_card_printer` | 1.0 | Card Printer machine that prints vanilla blank keycards |
| [PerkIconFit](mods/PerkIconFit) | `perk_icon_fit` | 1.0 | Fixes oversized pixel-art perk icons on the Starting Perks screen |
| [NextOpeningHighlightFix](mods/NextOpeningHighlightFix) | `next_opening_highlight_fix` | 1.0 | Stops a repeating NullReferenceException from Next Opening: Suppliers & Odin |
| [AlwaysHighlightTranslation](mods/AlwaysHighlightTranslation) | `always_highlight_translation` | 1.0 | Translation patch for the Always Highlight Mono mod |

## Install (players)

1. Copy a mod folder from `mods\<Name>` (manifest + DLL, plus any `Textures` / `Localization` folders) into
   `<Steam library>\steamapps\common\Probably Stolen\Mods\<Name>`.
2. Start the game, open the **Mods** panel, select the mod and press **Activate**.
3. Restart the game when prompted.

Or build and deploy in one step: `.\build-all.ps1 -Deploy` (see below).

## Build (modders)

Requirements: .NET SDK 6 or newer, and Probably Stolen installed.

The projects reference the game's managed assemblies through the `GameDir` property.
Set it once in `Directory.Build.props`, or pass it per build:

```powershell
dotnet build mods\SaveAnywhere\SaveAnywhere.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

Build every mod:

```powershell
.\build-all.ps1
```

Build and copy the results into the game's `Mods` folder:

```powershell
.\build-all.ps1 -Deploy
```

Details and troubleshooting: [BUILDING.md](BUILDING.md).

## Before publishing to the Workshop

Some manifests still carry placeholder values (`<Author>Local</Author>`, an older game version).
Update `<Author>`, `<GameVersions>` and `<Description>` in the manifest before uploading.
The Workshop uploader tool lives here:
https://github.com/lethecus/probably-stolen-workshop-uploader

## Notes

- These are unofficial mods; back up your saves before using anything that changes save data.
- Mods are loaded by the game's own mod loader; nothing here modifies game files on disk.
