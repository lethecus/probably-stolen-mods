# Auto-sync tools

Keeps the repository's `mods\` folder in sync with the folder where the mods are actually
edited and built, so the public source stays current without manual copying.

## How it works

`watch-sync.ps1` runs hidden in the background (started at logon) and uses a
**FileSystemWatcher** - there is **no timer and no polling**. Nothing happens while the sources
are untouched.

When a file under the source folder changes, it waits 20 seconds for the edits to settle and then
runs `sync-to-repo.ps1`, which:

1. Copies changed source files into `mods\<Mod>` (never `bin\`, `obj\`, `Logs\`; never deletes).
2. Regenerates each mod's `README.md` from its `manifest.xml`.
3. Commits as `Auto-sync mod sources` and pushes to `main` - only if git sees a difference.

Compiling a mod does not trigger a sync: build output is ignored. Syncing also runs once at
startup, which catches edits made while the watcher was not running and retries any commit whose
push failed earlier.

## Install

```powershell
.\tools\setup-autosync.ps1 -SourceRoot "<folder with one sub-folder per mod>"
```

Example:

```powershell
.\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"
```

This writes `tools\sync.config.json` and registers the scheduled task
**ProbablyStolenMods-AutoSync**:

- trigger: **at logon only** (no repetition)
- action: `wscript.exe` -> `tools\watch-sync.vbs` -> hidden `watch-sync.ps1`
- restart on failure, no execution time limit

The task never opens a console window.

Pushes authenticate through the GitHub CLI (`gh auth setup-git` is run for you). If `gh` is not
signed in, run `gh auth login` once.

## Start / test / stop

```powershell
Start-ScheduledTask -TaskName ProbablyStolenMods-AutoSync   # start without logging off

.\tools\sync-to-repo.ps1 -DryRun     # show what would change
.\tools\sync-to-repo.ps1             # sync, commit, push now
.\tools\sync-to-repo.ps1 -NoPush     # commit locally only

.\tools\setup-autosync.ps1 -Remove   # stop the watcher and unregister the task
```

Logs:

- `%LOCALAPPDATA%\PSModsAutoSync\watch.log` - watcher started, change detected, sync ran
- `%LOCALAPPDATA%\PSModsAutoSync\sync.log` - what was copied, committed, pushed

## Notes

- `tools\sync.config.json` is ignored by git because it contains local paths.
- If the sources move, run `setup-autosync.ps1` again with the new `-SourceRoot`.
- The watcher only runs while you are logged in. It starts again automatically at the next logon
  and syncs whatever changed in the meantime.
- Because the sync stages the whole working tree before committing, changes you make directly in
  the repository are picked up by the next sync as well.

## Nexus publishing (nexus-sync.ps1)

`sync-to-repo.ps1` now finishes by running `nexus-sync.ps1`, so one pass covers Workshop, the
repository and the Nexus page.

For every mod in `tools\nexus-mods.json` it asks the Nexus v3 API for the page version and the
MAIN file version. When either differs from `<ModVersion>` in the repo manifest, it:

1. builds the mod,
2. rebuilds the dist zip with `make-nexus-zip.ps1`,
3. pushes the new version through `..\PersonalModFixes\tools\nexus_push.py`, which uploads the
   zip, creates the version, sets the page version, archives the old file and adds the changelog
   from `dist\changelog-<prefix>-<version>.txt`.

A mod with no changelog for the new version is skipped and logged, never given invented text.

```powershell
.\tools\nexus-sync.ps1 -DryRun          # what is behind
.\tools\nexus-sync.ps1                  # build, zip and push everything behind
.\tools\nexus-sync.ps1 -Only FastCharger
.\tools\sync-to-repo.ps1 -NoNexus       # sync repo and Workshop-side work only
python ..\PersonalModFixes\tools\nexus_push.py --check   # read-only drift report
```

Log: `%LOCALAPPDATA%\PSModsAutoSync\nexus.log`.

Notes:

- `file_id` in `nexus-mods.json` is the legacy v1 (game scoped) id. v3 uses global ids, so the
  pusher resolves the mod and the file group at run time. Changing those ids by hand is not needed
  while `repo` and `changelog_prefix` are correct.
- The API can upload files to an existing mod but cannot create a mod page. A brand new mod still
  needs its page made on the website first; after that, add its `mod_id`, `file_id`, `repo` and
  `changelog_prefix` here and the automation will keep it current.
