<#
.SYNOPSIS
	Installs (or removes) the hidden watcher that keeps this repository in sync with the
	local mod sources.

.DESCRIPTION
	Registers a Windows scheduled task that starts tools\watch-sync.ps1 at logon through
	wscript.exe (no console window). The watcher uses a FileSystemWatcher, so it only does
	anything when a file in the source folder actually changes - there is no timer.

	Run it again with a different -SourceRoot to point the watcher elsewhere, or use
	-Remove to uninstall it.

.PARAMETER SourceRoot
	Folder that contains one sub-folder per mod (the folder with the .csproj and manifest.xml).

.PARAMETER Remove
	Stop and unregister the scheduled task.

.EXAMPLE
	.\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"

.EXAMPLE
	.\tools\setup-autosync.ps1 -Remove
#>
[CmdletBinding()]
param(
	[string]$SourceRoot,
	[switch]$Remove
)

$ErrorActionPreference = 'Stop'
$taskName = 'ProbablyStolenMods-AutoSync'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$configPath = Join-Path $scriptDir 'sync.config.json'

function Stop-Existing {
	$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
	if ($existing) {
		Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
		Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
		Start-Sleep -Seconds 2
	}
	# Remove a watcher that is still alive from a previous registration.
	Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -match 'watch-sync\.ps1' } |
		ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

if ($Remove) {
	Stop-Existing
	Write-Host "Removed scheduled task '$taskName'." -ForegroundColor Green
	return
}

if (-not $SourceRoot) {
	throw 'Pass -SourceRoot, e.g. .\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"'
}
if (-not (Test-Path $SourceRoot)) {
	throw "SourceRoot does not exist: $SourceRoot"
}
if (-not (Test-Path (Join-Path $scriptDir 'watch-sync.ps1'))) {
	throw 'Missing tools\watch-sync.ps1'
}

$config = [ordered]@{
	sourceRoot = (Resolve-Path $SourceRoot).Path
	repoRoot   = (Resolve-Path $repoRoot).Path
	branch     = 'main'
}
$config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8
Write-Host "Wrote $configPath"

# Make git pushes non-interactive by using the GitHub CLI credential helper (if available).
$gh = $null
foreach ($candidate in @(
		(Join-Path $env:LOCALAPPDATA 'gh-cli\bin\gh.exe'),
		'C:\Program Files\GitHub CLI\gh.exe',
		(Get-Command gh.exe -ErrorAction SilentlyContinue).Source
	)) {
	if ($candidate -and (Test-Path $candidate)) { $gh = $candidate; break }
}
if ($gh) {
	Push-Location $repoRoot
	try { & $gh auth setup-git 2>&1 | Out-Host }
	finally { Pop-Location }
	Write-Host 'Configured git to use the GitHub CLI credentials.'
}
else {
	Write-Warning 'GitHub CLI not found; make sure git can push to origin without prompting.'
}

$arguments = '//B //Nologo "{0}"' -f (Join-Path $scriptDir 'watch-sync.vbs')
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument $arguments -WorkingDirectory $repoRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn

$settings = New-ScheduledTaskSettingsSet `
	-StartWhenAvailable `
	-MultipleInstances IgnoreNew `
	-ExecutionTimeLimit ([TimeSpan]::Zero) `
	-AllowStartIfOnBatteries `
	-DontStopIfGoingOnBatteries `
	-RestartCount 999 `
	-RestartInterval (New-TimeSpan -Minutes 1) `
	-Hidden

Stop-Existing

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings `
	-Description 'Watches the Probably Stolen mod sources and syncs changes to the repository (no timer).' | Out-Null

Write-Host "Registered '$taskName' (starts at logon, syncs only when a mod file changes)." -ForegroundColor Green
Write-Host "Source: $((Resolve-Path $SourceRoot).Path)"
Write-Host "Repo:   $((Resolve-Path $repoRoot).Path)"
Write-Host ''
Write-Host 'Start it now without logging off:'
Write-Host "  Start-ScheduledTask -TaskName $taskName"
Write-Host ''
Write-Host "Logs: $env:LOCALAPPDATA\PSModsAutoSync\watch.log and sync.log"
