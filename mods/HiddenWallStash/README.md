# Hidden Wall Stash

A concealed 15×13 smuggler storage with ten tabs, installed at the start of every game. Hover the metal
grate on the wall above the shop radio to outline it; click it to open, or drag an item onto it to store it
without opening.

## Stash size

**Stash size → Cells per row / Rows per tab** (mod settings)

- range is 5 to 25 per side, defaults are 15 × 13
- the size applies on the next load, not mid-session, and it is applied in place on that single load
- **growing** always works; repeating cells are added, nothing is moved
- **shrinking** needs empty tabs. If anything is still inside, the resize is **refused as a whole**: the stash
  keeps its previous size, nothing is moved or deleted, and the log says how many items are in the way
- shrinking is refused rather than repacked on purpose: a tab that no longer fits an item has nowhere to put it,
  and the safe answer is to ask you to empty it first

The stash is not a placed object - it lives in the mod's own save data - so the size is applied to the existing
tab grids with the game's own `SetShape` + `Validate` (the same call the player inventory uses when a perk
changes its size).

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

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.

https://github.com/lethecus/probably-stolen-mods

## Translations

This mod ships its text in all eight languages the game supports:

    en  English (fallback for anything missing)
    zh  Simplified Chinese
    zh_TW  Traditional Chinese
    de  German
    fr  French
    ja  Japanese
    pl  Polish
    ru  Russian

Files live in `Localization\<locale>.csv` as `key,value` pairs. If a wording is wrong or awkward in your language, edit
that one line and send it back - no code, no rebuild, and I will fold it into the next version. The `en.csv` file is the
reference: every other locale has exactly the same keys.

Item names and descriptions are written into the save when the stash is created, so an existing stash keeps the language
it was made in. UI labels and tooltips follow the game language immediately.
