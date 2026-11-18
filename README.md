# Probably Stolen mod sources - for verification only

This repository exists for one reason: **so anyone can confirm that the mods we publish contain
nothing malicious.** The code here is the source the released DLLs are built from, published so
the build can be inspected, reproduced and compared instead of taken on trust.

- Read the source of any mod in [`mods/`](mods) - it is plain C# with no obfuscation.
- Build it yourself and compare the result: see [BUILDING.md](BUILDING.md).
- The mods only use the game's own mod API (`IMod`, `ModHook`, `ModHelper`) plus Harmony patches,
  and read/write only the game's normal files and folders.

## Liquid Locale Guard - Nexus only, on purpose

Several liquid mods (Auto-Name Liquid Bottles, Advanced Chemistry, Chemical Workbench, Ani The Way Back,
Probably Tampered, Water Jug Tools) fail together with `Attempting to use an invalid operation handle`, each
showing a caution in the mods menu. Only the first one to load is a real failure: the game asks the localization
system for liquid names while the mod list is still loading, and once that throws, liquid data is poisoned for
the rest of the session, so every later liquid mod inherits the same error.

[Liquid Locale Guard](mods/LiquidLocaleGuard) makes that one lookup fall back instead of throwing, then restores
the real names after the game has loaded. Source is in this repository like everything else.

**It is not on the Workshop, deliberately.** It has to run before the liquid mods, and Workshop items are
loaded in whatever order Steam reports - a mod cannot influence that. The Nexus download installs as
`Mods\!LiquidLocaleGuard`, and local folders are always loaded before Workshop items, so the leading `!` is what
guarantees it goes first. Renaming the folder removes that guarantee.

Get it on Nexus: <https://www.nexusmods.com/probablystolen> (mod name: **Liquid Locale Guard**).

Included mods: BlankCardPrinter, CalendarTV, CassetteRandomize, CassetteTapeSize,
ContaminantConverter, CustomerCounter, FastCharger, HiddenWallStash, LiquidLocaleGuard,
SaveTheNewspaper, TinkerChance, WaterJugTools.

Only released or release-ready mods live here - the Workshop ones, Liquid Locale Guard (Nexus only, for the
load order reason above), and Cage Expansion ahead of its release. Personal-use mods are deliberately kept out
of this repository.

## Please do not steal these

Do not reupload these mods, or their source, as your own work. Do not repackage them under a
different name or author.

If you want to learn from the code, build on it, or include part of it in your own mod, that is
fine under the license - but credit the original author and keep the source available, as
**GPL-3.0** requires (see [LICENSE](LICENSE)).

The official copies are the ones published on the Steam Workshop by the author of this
repository. Anything else is a copy.
