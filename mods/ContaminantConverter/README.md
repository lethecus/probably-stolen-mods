# Contaminant Converter

A powered chemistry machine with a battery slot and a standard module bay. Place a source and a target liquid container, select one contaminant, check the Product readout, then press Convert. Converts Chemical Contaminant to Acid or Zyanide (selectable), Organic Waste to Base, Microbes to Contraceptive Hormones, Mineral to N-Methylol Base, Heavy Metal to Lipobond Agent, and Physical Contaminant to Reducing Agent. Efficiency modules lower the power cost to a minimum of 1 per use; performance modules raise throughput. Not a starter item: Dr. Jackson's shop stocks one at random, and a debug spawn is provided. Includes a custom machine sprite.

- Mod ID: `contaminant_converter`
- Version: 1.2
- Author: Lethecus
- Game version: 1.0.0.14

## Install

Copy this folder into `<game>\Mods\ContaminantConverter`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and the built DLL next to each other (plus any `Textures`/
`Localization` folders).

## Build

```powershell
dotnet build ContaminantConverter.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1 -Deploy`.

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.
