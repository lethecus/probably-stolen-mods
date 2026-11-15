# Tinkerer Chance

Turns the Tinkerer perk into a setting instead of a fixed 20% chance: pick any keep-chance from 0 to 100% and it applies immediately.

- Press **F4** in game (rebindable) to open the settings window.
- **Keep chance** slider: 0-100%. At 0 you always consume the material; at 100 you always keep it.
- **Change hotkey**: click, then press any key; it is saved immediately.
- **Reset to 20%** restores the vanilla behaviour.

Both settings persist between sessions in `ModData\tinker_chance\settings.txt`, so they are global rather than tied to one save file and apply from the moment the mod is enabled.

## Coverage

| Upgrade path | Behaviour |
| --- | --- |
| Vanilla `StartingPerkContent.ConsumeUpgradeMaterial` (machines, modules, toolboxes, water tanks, reactors, cages, husbandry) | Patched to use the configured chance. |
| Scavenging Tool Upgrades (`ScavToolUpgrade`, workshop 3829922293) | Rolls its own hardcoded 20 in `Rules.Upgrade` and `NewTools.Spend`; both constants are transpiled when that mod is loaded. If it is not installed the patch is skipped silently - no error, no log line. |
| Gun repair kits | Unaffected. `GunHelper.TryRepairGun` destroys the kit unconditionally and never rolls Tinkerer. |

The `reflection-external` capability exists only for the Scavenging Tool Upgrades patch: it reflects into that one assembly and patches two named methods.

## Debug fallback

`dev.txt` in the game root enables the debug menu. Under **Tinkerer** there are presets for 0/20/50/100%, a "Log current chance" entry, and a test that consumes a spawned scrap.

- Mod ID: `tinker_chance`
- Version: 1.2
- Game version: 1.0.0.14
