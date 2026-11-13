<#
.SYNOPSIS
	Mirrors mod sources into this repository, commits the changes and pushes them.

.DESCRIPTION
	The mod sources are edited/built in a working folder (by default the build folder used
	during development). This script copies the source files of every mod that already exists
	in .\mods, regenerates the per-mod README files, then commits and pushes if anything changed.

	It never copies build output (bin/obj/Logs) and never deletes files in the repository.

.PARAMETER SourceRoot
	Folder containing one sub-folder per mod (with the .csproj and manifest.xml).
	Normally supplied by tools\sync.config.json.

.PARAMETER RepoRoot
	Repository working tree. Defaults to the parent of this script's folder.

.PARAMETER DryRun
	Show what would change without writing, committing or pushing.

.PARAMETER NoPush
	Commit locally but do not push.

.EXAMPLE
	.\tools\sync-to-repo.ps1 -DryRun
#>
[CmdletBinding()]
param(
	[string]$SourceRoot,
	[string]$RepoRoot,
	[string]$ConfigPath,
	[switch]$DryRun,
	[switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $scriptDir }
if (-not $ConfigPath) { $ConfigPath = Join-Path $scriptDir 'sync.config.json' }

$branch = 'main'
$exclude = @()
if (Test-Path $ConfigPath) {
	$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
	if (-not $SourceRoot -and $config.sourceRoot) { $SourceRoot = $config.sourceRoot }
	if ($config.repoRoot -and -not $PSBoundParameters.ContainsKey('RepoRoot')) { $RepoRoot = $config.repoRoot }
	if ($config.branch) { $branch = $config.branch }
	if ($config.exclude) { $exclude = @($config.exclude) }
}

if (-not $SourceRoot) {
	throw "No SourceRoot. Pass -SourceRoot or set sourceRoot in $ConfigPath"
}
if (-not (Test-Path $SourceRoot)) {
	throw "SourceRoot does not exist: $SourceRoot"
}

$logDir = Join-Path $env:LOCALAPPDATA 'PSModsAutoSync'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir 'sync.log'
if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 1MB) {
	Move-Item $logFile ($logFile + '.old') -Force
}

function Write-Log([string]$message) {
	$line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $message
	Add-Content -Path $logFile -Value $line
	Write-Host $line
}

$git = (Get-Command git.exe -ErrorAction SilentlyContinue).Source
if (-not $git) { $git = 'C:\Program Files\Git\cmd\git.exe' }
if (-not (Test-Path $git)) { throw 'git.exe not found' }

# One run at a time (scheduled task can overlap with a manual run).
$mutex = New-Object System.Threading.Mutex($false, 'Global\PSModsAutoSync')
if (-not $mutex.WaitOne(0)) {
	Write-Log 'Another sync is already running, skipping.'
	return
}

# Pushes any local commits that origin does not have yet (retries after a failed push).
function Push-Pending {
	$previous = $ErrorActionPreference
	$ErrorActionPreference = 'Continue'
	try {
		& $git fetch origin $branch --quiet 2>&1 | Out-Null
		if ($LASTEXITCODE -ne 0) {
			Write-Log 'Fetch failed; will retry on the next run.'
			return
		}
		$ahead = [int]((& $git rev-list --count "origin/$branch..HEAD") | Select-Object -First 1)
		if ($ahead -le 0) {
			return
		}
		Write-Log "Pushing $ahead pending commit(s)..."
		$push = & $git push origin $branch 2>&1 | ForEach-Object { $_.ToString() }
		$pushCode = $LASTEXITCODE
		$push | ForEach-Object { Write-Log "  $_" }
		if ($pushCode -ne 0) {
			Write-Log 'Push failed. The commit stays local and the next run will retry.'
			return
		}
		Write-Log 'Pushed to origin.'
	}
	finally {
		$ErrorActionPreference = $previous
	}
}

try {
	$modsDir = Join-Path $RepoRoot 'mods'
	if (-not (Test-Path $modsDir)) { throw "No mods folder in $RepoRoot" }

	$knownMods = Get-ChildItem $modsDir -Directory |
		Where-Object { $exclude -notcontains $_.Name } |
		Select-Object -ExpandProperty Name
	$copied = 0
	$normalised = 0

	foreach ($mod in $knownMods) {
		$source = Join-Path $SourceRoot $mod
		if (-not (Test-Path $source)) { continue }
		$destination = Join-Path $modsDir $mod
		if (-not (Test-Path $destination)) { New-Item -ItemType Directory -Force -Path $destination | Out-Null }

		$files = Get-ChildItem $source -Recurse -File |
			Where-Object { $_.FullName -notmatch '\\(bin|obj|Logs)\\' }

		foreach ($file in $files) {
			$relative = $file.FullName.Substring($source.Length).TrimStart('\')
			if ($relative -ieq 'README.md') { continue }
			$target = Join-Path $destination $relative
			New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null

			$needsCopy = $true
			if (Test-Path $target) {
				$existing = Get-Item $target
				if ($existing.Length -eq $file.Length -and
					[math]::Abs(($existing.LastWriteTimeUtc - $file.LastWriteTimeUtc).TotalSeconds) -lt 1) {
					$needsCopy = $false
				}
			}
			if ($needsCopy) {
				if (-not $DryRun) { Copy-Item $file.FullName $target -Force }
				$copied++
			}
		}

		# Keep the per-mod README in sync with the manifest.
		$manifestPath = Join-Path $destination 'manifest.xml'
		if (Test-Path $manifestPath) {
			[xml]$manifest = Get-Content $manifestPath -Raw
			$m = $manifest.Manifest
			$csproj = (Get-ChildItem $destination -Filter *.csproj | Select-Object -First 1).Name
			$readmePath = Join-Path $destination 'README.md'
			$text = @"
# $($m.Name)

$($m.Description)

- Mod ID: ``$($m.ID)``
- Version: $($m.ModVersion)
- Author: $($m.Author)
- Game version: $($m.GameVersions.Version)

## Install

Copy this folder into ``<game>\Mods\$mod``, activate the mod in the game's Mods panel and restart.
The folder needs ``manifest.xml`` and the built DLL next to each other (plus any ``Textures``/
``Localization`` folders).

## Build

``````powershell
dotnet build $csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
``````

Or from the repository root: ``.\build-all.ps1 -Deploy``.
"@
			$current = if (Test-Path $readmePath) { Get-Content $readmePath -Raw } else { '' }
			if ($current -ne $text) {
				if (-not $DryRun) {
					[System.IO.File]::WriteAllText($readmePath, $text, (New-Object System.Text.UTF8Encoding($false)))
				}
				$normalised++
			}
		}
	}

	if ($copied -eq 0 -and $normalised -eq 0 -and -not $DryRun) {
		# Nothing to copy; git may still have changes (manual edits in the repo).
	}

	Push-Location $RepoRoot
	try {
		$status = & $git status --porcelain
		if (-not $status) {
			Write-Log "No changes. (copied=$copied readme=$normalised)"
			if (-not $DryRun) { Push-Pending }
			return
		}

		Write-Log ("Changes detected: {0} file(s) to sync, {1} README update(s)." -f $copied, $normalised)
		$status | ForEach-Object { Write-Log "  $_" }

		if ($DryRun) {
			Write-Log 'Dry run: nothing written, committed or pushed.'
			return
		}

		& $git add -A
		$changed = (& $git diff --cached --name-only)
		if (-not $changed) {
			Write-Log 'Nothing staged after add, stopping.'
			return
		}

		$email = (& $git config user.email)
		if (-not $email) {
			Write-Log 'ERROR: git user.email is not configured for this repository; cannot commit.'
			Write-Log '       Run: git -C "<repo>" config user.name "Your Name" ; git -C "<repo>" config user.email "you@example.com"'
			return
		}

		$summary = ($changed | Select-Object -First 12) -join ', '
		if ($changed.Count -gt 12) { $summary += ", +$($changed.Count - 12) more" }
		& $git commit -m 'Auto-sync mod sources' -m "Changed: $summary" | Out-Null
		if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }
		Write-Log "Committed $($changed.Count) file(s)."

		if ($NoPush) {
			Write-Log 'NoPush set, commit left local.'
			return
		}

		Push-Pending
	}
	finally {
		Pop-Location
	}
}
finally {
	$mutex.ReleaseMutex()
}
