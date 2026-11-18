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

## Using with AutoStorage

The stash counts as shop storage, so AutoStorage can use it as a sorting destination:

- open AutoStorage's container list with **Ctrl+L**
- it appears as **Hidden Wall Stash**, once per storage page - ten entries, `tab 1` to `tab 10`
- configure a page from the **Settings** button on its row

**Shift+right-click will not open it.** That gesture asks the game for the item under the mouse cursor, and the
stash has no item on screen - only the wall grate - so there is nothing for it to hit. The container list is the
only way in, and the same settings window opens from there.

**Why, exactly:** the two paths look for containers in different ways. The click path is spatial - it calls
`RenderHandler.RaycastElement<GameItem>(mousePosition)`, i.e. "which drawn item is under the cursor?", and the
stash is never drawn as an item. The list path is logical - it enumerates `PlayerStore.FindAllItem()`, and that
is the list this mod adds the stash to. So it can be found by enumerating and never by pointing.

Note for save editors and other mods: the stash's internal identifier is **`wall_grate_stash`**, while the item
still displays as "Hidden Wall Stash". AutoStorage ignores any id containing certain words - `hidden` among
them - so the old identifier could never be a sorting target. Saves written before this change are migrated
automatically.

## Uninstalling

Empty the stash before disabling this mod, or its contents are lost.

## Source

https://github.com/lethecus/probably-stolen-mods
