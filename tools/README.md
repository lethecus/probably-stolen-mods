# Auto-sync tools

These scripts keep the repository's `mods\` folder in sync with the folder where the mods are
actually edited and built, so the public source stays current without manual copying.

## How it works

`sync-to-repo.ps1`:

1. Looks at each mod folder that already exists in `mods\`.
2. Copies changed source files from the working folder into the repository (never `bin\`, `obj\`
   or `Logs\`, never deletes anything).
3. Regenerates each mod's `README.md` from its `manifest.xml`.
4. If git reports changes, commits them as `Auto-sync mod sources` and pushes to `main`.

It does nothing when there are no changes, and it refuses to run twice at the same time.

`setup-autosync.ps1` writes `tools\sync.config.json` and registers the Windows scheduled task
**ProbablyStolenMods-AutoSync**, which runs the sync at logon and every few minutes.

## Install

```powershell
.\tools\setup-autosync.ps1 -SourceRoot "<folder with one sub-folder per mod>"
```

Example:

```powershell
.\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"
```

Change the interval with `-EveryMinutes 5`.

Pushes authenticate through the GitHub CLI (`gh auth setup-git` is run for you). If `gh` is not
signed in, run `gh auth login` once.

## Test / run manually

```powershell
.\tools\sync-to-repo.ps1 -DryRun     # show what would change
.\tools\sync-to-repo.ps1             # sync, commit, push
.\tools\sync-to-repo.ps1 -NoPush     # commit locally only
```

Log file: `%LOCALAPPDATA%\PSModsAutoSync\sync.log`

## Remove

```powershell
.\tools\setup-autosync.ps1 -Remove
```

## Notes

- `tools\sync.config.json` is ignored by git because it contains local paths.
- If the sources move, run `setup-autosync.ps1` again with the new `-SourceRoot`.
- The task only runs while you are logged in (it is registered for your user account).
- The repository also records changes you make to it directly, because the sync stages the whole
  working tree before committing.
