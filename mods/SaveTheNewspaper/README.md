# Save the Newspaper

The morning newspaper deletes itself at the end of the day. Not when you throw it away - it destroys
itself while you sleep, wherever it is. That is not a bug you can work around by storing it better: the
item carries its own destroy callback, and the night pass runs it for every owned item it can find, so a
paper tucked inside a dossier or a storage bay is found and removed just the same.

This mod keeps it. One prefix on `PlayerStore.EndNight` clears that callback on any newspaper before the
night pass runs, so the paper you put away is still there in the morning.

## The setting

**Keep the newspaper overnight** (mod settings, on by default)

- **On** - papers you store stay stored. You still get a fresh one each morning.
- **Off** - new papers delete themselves again. Papers you already saved are kept; the setting never
  reaches back and removes anything.

## What it does not do

- It does not change where the paper arrives. Vanilla still drops it on the front counter, or in the trash
  can if **Send Newspaper to Trash Can** is enabled in the game's options.
- It does not touch the counter itself. A paper left lying on the front counter is still destroyed by
  `ClearFrontInv` at night, exactly as in vanilla - put it away and it survives.
- No links, saves, machines or other items are touched. `caps=none`.

## Why keep newspapers?

Other mods add uses for them, and the paper disappearing before you can use it is the reason this exists.
It also works fine alongside the vanilla trash option: papers in the trash are not scanned by the night
pass, and are only cleared if the janitorial service runs.

## Install

Copy the `SaveTheNewspaper` folder into `Probably Stolen\Mods\`. Requires no other mod.

## Source

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.

https://github.com/lethecus/probably-stolen-mods
