# Liquid Locale Guard

Fixes the `Attempting to use an invalid operation handle` cascade that breaks several liquid mods at once.

## The failure, as measured on 18 Nov 2026

Two sessions, same mods, only the load order differed:

| session | first mod to fail | result |
|---|---|---|
| before | Tobacco Tweaks | 6 other liquid mods inherit the error |
| after removing it | Auto-Name Liquid Bottles | 5 other liquid mods inherit the error |

The chain:

```
any mod patches a method whose body touches Liquid
  -> Liquid..cctor runs Liquid.InitLiquid()
    -> LocHelper.GetLocalizedMechanic("liquid_water")
      -> Unity Localization -> Addressables WaitForCompletion
        -> localization not ready during ModLoader.Awake  -> throws
          -> Liquid's type initializer fails
            -> .NET caches that failure for the process
              -> every later liquid mod throws the identical exception
```

That is why disabling one mod just moves the caution to the next one: it isn't the mod, it's being first.

## What this mod does

1. **Load guard** — a Harmony finalizer on `LocHelper.GetLocalizedMechanic`. If the call throws *and* the key starts with `liquid_`, it returns a readable English fallback (`liquid_heavy_metal` → `Heavy Metal`) instead of re-throwing. `Liquid` then initialises once, successfully, and nothing is poisoned.
2. **Name repair** — on `OnGameLoadedLate` and again on `OnWakingUpLate`, the real localized names are read from the table and written onto the same `Liquid` objects (`displayName`), so the UI never shows a placeholder. Identifier-to-key mapping is the 36 entries in `Liquid.InitLiquid()`.

Nothing else changes: no other localization key is affected, no exception is swallowed unless it is a liquid key, and no capabilities are declared.

## When it can and cannot help

* **Helps:** any mod that patches a Liquid-touching method during load — Auto-Name Liquid Bottles, Advanced Chemistry, Ani, Chemical Workbench, Probably Tampered, Water Jug Tools, and this one's siblings.
* **Does not help:** a genuinely missing localization table, or a mod that fails for another reason after load.
* **If the game changes the signature** of `GetLocalizedMechanic`, the patch silently stops applying; the mod logs nothing and changes nothing, rather than making things worse.

## Load order: what actually controls it

Measured from `ModLoader.LoadDiscoveredMods()` and the 18 Nov log, not from the mods menu:

```
1. local mods from Mods\\  (Directory.GetDirectories order, effectively alphabetical)
2. Workshop items          (Steam's installed-item order)
3. prerequisites are hoisted above the mods that list them
```

The mods menu order (`modOrder` in SaveFile.es3) is **display order and the enabled list only** - it does
not change what loads first. So:

* **Manual install into `Mods\\`** always loads before every Workshop mod. This is the reliable route.
* **Workshop install** is best effort: it loads wherever Steam's list puts it, which a new subscription
  cannot control.
* **The clean fix** is for liquid mods to list `liquid_locale_guard` in `<Prerequisites>`. The loader hoists
  prerequisites automatically, so it then loads first no matter where the files sit.

This mod pre-warms `Liquid` in its own `OnEnable`. With the guard active that initialisation succeeds, and
every liquid mod loading afterwards finds a working type. If it logs *"Liquid was already initialised and had
failed before this mod loaded"*, it lost the race and the session needs a restart with it earlier.

## Install: keep the folder name exactly as shipped

The shipped folder is **`!LiquidLocaleGuard`**. The leading `!` is not decoration - it is what guarantees the
load position:

* local mods are enumerated with `Directory.GetDirectories(Mods)`, which returns **name-sorted** on NTFS
  (`!` 0x21 < `0` 0x30 < `A` 0x41), and they all load **before** any Workshop item;
* so `!LiquidLocaleGuard` loads before `AutoNameLiquidBottles`, `AdvancedChemistry`, `AniTheWayBack`,
  `ChemicalWorkbench`, `ProbablyTampered` and `WaterJugTools`, whether those are local or from Steam.

Renaming the folder to `LiquidLocaleGuard` puts it after `AutoNameLiquidBottles` and it will lose the race.
The mod id in `manifest.xml` is what the game stores, so renaming does not corrupt enablement - it just changes
when the mod loads.

## If you found this through the Workshop

A Workshop item is unpacked into `steamapps\workshop\content\4348910\<published id>\`, and the game asks
Steam for the list of subscribed items - so a Workshop copy cannot control where it lands in the load order.
A Workshop copy is therefore best effort only.

If it does not help, take the Nexus download, unzip it so that `Mods\!LiquidLocaleGuard\manifest.xml` exists,
and leave the Workshop copy subscribed if you like. When two copies share a mod id the loader logs
*"the local copy wins over the Workshop copy"* and the local folder is the one that runs - and local folders are
loaded before every Workshop item, with `!` first among them. That is the guaranteed route.

## Testing

1. Enable with all the liquid mods on, restart.
2. `Player.log` should contain **zero** `invalid operation handle (Harmony patching or Init)` lines.
3. Every previously failing mod should log `Initialized:` and `running=True` in its audit line.
4. Water and other liquid tooltips should show normal names, not `liquid_water`.
5. Sleep one night; the repair retries on waking.

## Publishing notes

Publishable: it is a narrow workaround for a game-side ordering bug, useful to anyone running more than one liquid mod. Before upload it needs a preview image and a Nexus zip; version 1.0.0 on repo, Workshop and Nexus.

Worth linking the finding to the game's modding channel: `Liquid`'s static initializer should use `LocHelper.GetLocalizedMechanicOrDefault`, or mods should defer patches as `TobaccoExpansion` does with its `DeferredPatches` class.
