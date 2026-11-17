# Blank Card Printer

Adds a Card Printer machine that prints vanilla blank keycards. It needs a battery in its power slot;
the Print button shows the price before you press it. Find it in scavenging or buy it from
the inventor.

Easy mode (default) keeps the original cost: 3 power and 10 credits per card, no material. Normal mode
adds a filament slot and uses 1 unit of 3D Printing Filament per card, plus 6 power and 10 credits - so it stops
being a way to print credits into cards. The mode is chosen in the mod settings and applies after a
reload.

https://github.com/lethecus/probably-stolen-mods
The source for all my mods if you wish to confirm nothing shady going on.

- Mod ID: `blank_card_printer`
- Version: 1.3
- Author: Lethecus
- Game version: 1.0.0.14

## Settings

| Setting | Default | Notes |
| --- | --- | --- |
| Balance -> Easy mode | on | On: 3 power + 10 credits per card. Off: normal mode |

Normal mode (Easy mode off) adds a **3D Filament** panel to the machine. The slot accepts anything with
the game's `PRINTER_MATERIAL_TAG`, i.e. 3D Printing Filament, including modded versions.
Each print takes one unit off the stack.

The machine window is built when the printer is created, so switching modes needs the printer
re-spawned or the save reloaded.

## Install

Copy this folder into `<game>\Mods\BlankCardPrinter`, activate the mod in the game's Mods panel and
restart. The folder needs `manifest.xml`, `BlankCardPrinter.dll` and the `Textures` folder.

## Build

```powershell
dotnet build BlankCardPrinter.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1`.
