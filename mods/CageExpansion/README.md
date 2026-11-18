# Cage Expansion

Raises the animal cage's size cap so one cage can hold more rats before overcrowding starts hurting them.

## What it actually changes

The game's cage is a grid 10 cells wide and `3 + size` tall. `size` starts at 0 and is upgraded one row per
metal ingot. Vanilla stops at `size = 8`, and it is capped in **two** places:

| game code | vanilla behaviour |
|---|---|
| `AnimalCage.CanUpgradeSize` | `CAGE_SIZE_TAG < 8` — the tooltip and the upgrade action both ask this |
| `AnimalCage.UpgradeSize` | `Mathf.Min(size + 1, 8)` — a second clamp, so raising only the first would say "upgradeable" and then do nothing |
| `AnimalCage.CreateTooltip` | `8 - size` — the "expandable N more times" hint |

This mod redirects those three constants to a configurable cap. Default **12 rows**, range 8–16. Costs and
durability are untouched: one ingot per row, +20 max health per row, +40 item value per row.

## Why more rows fixes overcrowding

Overcrowding is not a flag set by the game per animal, it is occupancy measured by
`HusbandryHelper.GetAnimalOvercrowdingLevel(GameInventory)`:

```
occupancy = sum(size of every ANIMAL_ITEM_TAG item in the cage) / (grid width * grid height)

occupancy > 0.55  -> level 1  (warning)
occupancy > 0.70  -> level 2  (5% nightly harm roll)
occupancy > 0.90  -> level 3  (15% nightly harm roll)
```

A 10x8 cage is 80 cells. A 10x12 cage is 120 cells, so the same rats sit at two thirds of the percentage. Nothing
in the animal path is patched, so breeding, hunger, waste and escape chance behave normally.

## Settings

* **Maximum cage size** — tallest the cage can be upgraded to, in rows. Vanilla 8, default 12, maximum 16.

Changing it mid-save is safe: it only changes how many more rows can be bought. Already-bought rows stay.

## Compatibility

* **RAS Tank / RAS Machine Upgrade** — no overlap. They patch `WaterTankHelper` and `MachineRAS`; this patches
  `AnimalCage`.
* **Helper NPC plans** — the rat job reads `AnimalCage`/`HusbandryHelper` state; a taller cage only changes the
  occupancy percentage it reports, so the job should report the percentage rather than a headcount.
* **Vanilla Overhaul** — no shared methods.
* Nothing is added to the item directory, no assets, no capabilities.

## Uninstalling

Nothing is destroyed. A cage already upgraded past 8 keeps its rows and its contents; without the mod the game
just refuses further upgrades, and the tooltip shows the vanilla "max size" line. If you want the old size back,
empty the cage and reinstall the mod at 8 rows — do not shrink a full cage.

## Test checklist

1. Fresh or existing save, garage cage present.
2. Tooltip says "expandable N more times" counting to the configured cap, not to 8.
3. Upgrade past 8 with ingots: the grid grows one row each time, durability rises by 20, value by 40.
4. At 9+ rows the window still opens, scrolls and accepts drops in the new rows.
5. Put rats in until the tooltip's overcrowding line disappears below the level-1 threshold; sleep several nights
   and confirm no harm events.
6. Set the cap back to 8 with a cage at 12 rows: no upgrade offered, no error, cage still works.

## Publishing notes (not yet published)

Same-version rule: repo 1.0.0 = Workshop 1.0.0 = Nexus 1.0.0. Needs a preview image before upload; the manifest
description is already written for the store page.
