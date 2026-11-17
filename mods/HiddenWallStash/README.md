# Hidden Wall Stash

A concealed 15×13 smuggler storage with ten tabs, installed at the start of every game. Hover the metal
grate on the wall above the shop radio to outline it; click it to open, or drag an item onto it to store it
without opening.

## Stash size

**Stash size → Cells per row / Rows per tab** (mod settings)

- range is 5 to 25 per side, defaults are 15 × 13
- the size applies on the next load, not mid-session
- **growing** keeps everything, the stash is rebuilt at the new size and the contents move across
- **shrinking** repacks each tab first, then moves any overflow into free space in other tabs, so a smaller
  size never deletes anything
- if the items cannot all fit, the resize is **refused as a whole**: the stash keeps its previous size, every
  item is put back, and the log says which item did not fit

The stash is not a placed object - it lives in the mod's own save data - so changing size rebuilds the item
rather than trying to resize grids inside a window laid out for the old dimensions.

## Uninstalling

Empty the stash before disabling this mod, or its contents are lost.

## Source

https://github.com/lethecus/probably-stolen-mods
