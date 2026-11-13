<#
.SYNOPSIS
	Watches the mod source folder and syncs to the repository only when something changed.

.DESCRIPTION
	Runs hidden (started by tools\watch-sync.vbs through Task Scheduler at logon).
	It uses a FileSystemWatcher instead of a timer, so nothing happens while the sources
	are untouched. After the last change it waits a few seconds (debounce), then runs
	tools\sync-to-repo.ps1, which commits and pushes if git sees a difference.

	Build output (bin, obj, Logs) is ignored, so compiling a mod does not trigger a sync
	unless source files really changed.

.PARAMETER SourceRoot
	Folder with one sub-folder per mod. Defaults to sourceRoot in tools\sync.config.json.

.PARAMETER DebounceSeconds
	How long the folder must stay quiet before syncing. Default 20 seconds.
#>
[CmdletBinding()]
param(
	[string]$SourceRoot,
	[string]$ConfigPath,
	[int]$DebounceSeconds = 20
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ConfigPath) { $ConfigPath = Join-Path $scriptDir 'sync.config.json' }

if (-not $SourceRoot) {
	if (Test-Path $ConfigPath) {
		$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
		$SourceRoot = $config.sourceRoot
	}
}
if (-not $SourceRoot) { throw 'No SourceRoot and no tools\sync.config.json' }
if (-not (Test-Path $SourceRoot)) { throw "SourceRoot does not exist: $SourceRoot" }

$logDir = Join-Path $env:LOCALAPPDATA 'PSModsAutoSync'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$watchLog = Join-Path $logDir 'watch.log'

function Write-WatchLog([string]$message) {
	$line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $message
	Add-Content -Path $watchLog -Value $line
}

# Only one watcher at a time.
$mutex = New-Object System.Threading.Mutex($false, 'Global\PSModsWatchSync')
if (-not $mutex.WaitOne(0)) {
	Write-WatchLog 'Watcher already running, exiting.'
	return
}

$syncScript = Join-Path $scriptDir 'sync-to-repo.ps1'
if (-not (Test-Path $syncScript)) { throw "Missing $syncScript" }

$state = [hashtable]::Synchronized(@{
		Pending    = $false
		LastChange = [datetime]::UtcNow
	})

function Invoke-Sync([string]$reason) {
	Write-WatchLog "Syncing ($reason)."
	try {
		& $syncScript
	}
	catch {
		Write-WatchLog ("Sync failed: " + $_.Exception.Message + " - retrying in 5 minutes.")
		$state.Pending = $true
		$state.LastChange = [datetime]::UtcNow.AddMinutes(5)
	}
}

# Catch anything that changed while the watcher was not running (logoff, crash, reboot).
Invoke-Sync 'startup'

$resolved = (Resolve-Path $SourceRoot).Path
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $resolved
$watcher.IncludeSubdirectories = $true
$watcher.Filter = '*.*'
$watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor
	[System.IO.NotifyFilters]::LastWrite -bor
	[System.IO.NotifyFilters]::Size -bor
	[System.IO.NotifyFilters]::DirectoryName

$onChange = {
	$shared = $Event.MessageData
	$path = $Event.SourceEventArgs.FullPath
	if ($path -match '\\(bin|obj|Logs)\\') { return }
	$shared.Pending = $true
	$shared.LastChange = [datetime]::UtcNow
}

$subscriptions = @()
foreach ($eventName in 'Changed', 'Created', 'Deleted', 'Renamed') {
	$subscriptions += Register-ObjectEvent -InputObject $watcher -EventName $eventName -Action $onChange -MessageData $state
}
$watcher.EnableRaisingEvents = $true

Write-WatchLog "Watching $resolved (debounce ${DebounceSeconds}s)."

try {
	while ($true) {
		Start-Sleep -Seconds 2
		if (-not $state.Pending) { continue }
		if (([datetime]::UtcNow - $state.LastChange).TotalSeconds -lt $DebounceSeconds) { continue }
		$state.Pending = $false
		Invoke-Sync 'change detected'
	}
}
finally {
	$watcher.EnableRaisingEvents = $false
	foreach ($subscription in $subscriptions) {
		Unregister-Event -SubscriptionId $subscription.Id -ErrorAction SilentlyContinue
	}
	$watcher.Dispose()
	Write-WatchLog 'Watcher stopped.'
	$mutex.ReleaseMutex()
}
