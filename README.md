# Probably Stolen mod sources - for verification only

This repository exists for one reason: **so anyone can confirm that the mods we publish contain
nothing malicious.** The code here is the source the released DLLs are built from, published so
the build can be inspected, reproduced and compared instead of taken on trust.

- Read the source of any mod in [`mods/`](mods) - it is plain C# with no obfuscation.
- Build it yourself and compare the result: see [BUILDING.md](BUILDING.md).
- The mods only use the game's own mod API (`IMod`, `ModHook`, `ModHelper`) plus Harmony patches,
  and read/write only the game's normal files and folders.

Included mods: AutoSorterPlus, CassetteRandomize, CassetteTapeSize, ContaminantConverter,
WaterJugTools, CustomerCounter, HiddenWallStash, BlankCardPrinter, NextOpeningHighlightFix,
TinkerChance.

Not included: some mods are kept for personal use only and are intentionally not published here.

## Please do not steal these

Do not reupload these mods, or their source, as your own work. Do not repackage them under a
different name or author.

If you want to learn from the code, build on it, or include part of it in your own mod, that is
fine under the license - but credit the original author and keep the source available, as
**GPL-3.0** requires (see [LICENSE](LICENSE)).

The official copies are the ones published on the Steam Workshop by the author of this
repository. Anything else is a copy.
