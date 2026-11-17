# Fast Charger

Have you ever thought how the hell are we in space but lost fast charge compatibilities we already
had? Well then Fast Chargers is the mod you want. It adds a new Fast Charger and Fast Bloc. Both of
them have a mod menu setting to choose the amount of charge that happens per night. Also a setting
for animated texture effects. The game only loads the settings on load, so no changing mid-day.

https://github.com/lethecus/probably-stolen-mods
The source for all my mods if you wish to confirm nothing shady going on.

- Mod ID: `fast_charger`
- Version: 1.7
- Author: Lethecus
- Game version: 1.0.0.14

## What it adds

| Item | Footprint | Value | Behaviour |
| --- | --- | --- | --- |
| **Fast Charger** | 8x2, eight battery bays | 450 | Restores the configured amount to every battery inside it each machine cycle |
| **Fast Bloc** | 2x2 | 600 | Restores the configured amount to adjacent batteries and to nearby machines' internal cells |

Both use the vanilla cycle hooks and the vanilla power gate - same timing as the recharger and
powerblock, only the amount changes. Vanilla for reference: recharger 3 per cycle (6 with Commercial
Power), powerblock 2 (4).

Both are luxury items and are sold by **Dr. Jackson**:

- 20% chance from the first day his shop is accessible,
- +2% for every 5 days since, capped at 40%,
- the charger and the bloc roll independently, so one, both or neither can appear on a visit.

## Settings

The mod's own settings page (Mods -> Fast Charger):

| Setting | Default | Range | Notes |
| --- | --- | --- | --- |
| Fast charger -> Charge per cycle | 30 | 0-500 | 0 turns charging off |
| Fast bloc -> Charge per cycle | 20 | 0-500 | |
| Appearance -> Animate rainbow | on | on/off | The rainbow colours scroll across the machines |
| Appearance -> Animation speed | 8 | 4-20 fps | |

Settings are read once per load, so a restart is needed before a change takes effect.

Batteries are drawn inside the bays and tuck under the top panel. The rainbow animation restarts with the save, so freshly spawned or loaded machines both scroll.

## Install

Copy this folder into `<game>\Mods\FastCharger`, activate the mod in the game's Mods panel and
restart. The folder needs `manifest.xml`, `FastCharger.dll` and the `Textures` folder.

## Build

```powershell
dotnet build FastCharger.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Or from the repository root: `.\build-all.ps1`.
