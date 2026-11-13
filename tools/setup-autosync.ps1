<#
.SYNOPSIS
	Installs (or removes) the scheduled task that keeps this repository in sync with the
	local mod sources.

.DESCRIPTION
	Registers a Windows scheduled task that runs tools\sync-to-repo.ps1 at logon and every
	few minutes. The script mirrors changed mod sources into .\mods, commits and pushes them.

	Run it again with a different -SourceRoot to point the sync somewhere else, or use
	-Remove to uninstall the task.

.PARAMETER SourceRoot
	Folder that contains one sub-folder per mod (the folder with the .csproj and manifest.xml).

.PARAMETER EveryMinutes
	How often the sync runs. Default 2 minutes.

.PARAMETER Remove
	Unregister the scheduled task and stop syncing.

.EXAMPLE
	.\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"

.EXAMPLE
	.\tools\setup-autosync.ps1 -Remove
#>
[CmdletBinding()]
param(
	[string]$SourceRoot,
	[int]$EveryMinutes = 2,
	[switch]$Remove
)

$ErrorActionPreference = 'Stop'
$taskName = 'ProbablyStolenMods-AutoSync'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$syncScript = Join-Path $scriptDir 'sync-to-repo.ps1'
$configPath = Join-Path $scriptDir 'sync.config.json'

if ($Remove) {
	$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
	if ($existing) {
		Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
		Write-Host "Removed scheduled task '$taskName'." -ForegroundColor Green
	}
	else {
		Write-Host "Scheduled task '$taskName' was not installed."
	}
	return
}

if (-not $SourceRoot) {
	throw 'Pass -SourceRoot, e.g. .\tools\setup-autosync.ps1 -SourceRoot "D:\dev\probably-stolen\build"'
}
if (-not (Test-Path $SourceRoot)) {
	throw "SourceRoot does not exist: $SourceRoot"
}
if (-not (Test-Path $syncScript)) {
	throw "Missing $syncScript"
}

# Persist the configuration used by the scheduled task.
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
	try {
		& $gh auth setup-git 2>&1 | Out-Host
	}
	finally { Pop-Location }
	Write-Host 'Configured git to use the GitHub CLI credentials.'
}
else {
	Write-Warning 'GitHub CLI not found; make sure git can push to origin without prompting.'
}

$arguments = '//B //Nologo "{0}"' -f (Join-Path $scriptDir 'run-sync.vbs')
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument $arguments -WorkingDirectory $repoRoot

$atLogon = New-ScheduledTaskTrigger -AtLogOn
$repeat = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
	-RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes) `
	-RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
	-StartWhenAvailable `
	-MultipleInstances IgnoreNew `
	-ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
	-AllowStartIfOnBatteries `
	-DontStopIfGoingOnBatteries `
	-Hidden

$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existing) {
	Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger @($atLogon, $repeat) -Settings $settings `
	-Description 'Mirrors local Probably Stolen mod sources into the repository and pushes them.' | Out-Null

Write-Host "Registered '$taskName' (runs at logon and every $EveryMinutes minute(s))." -ForegroundColor Green
Write-Host "Source: $((Resolve-Path $SourceRoot).Path)"
Write-Host "Repo:   $((Resolve-Path $repoRoot).Path)"
Write-Host ''
Write-Host 'Test it now:'
Write-Host '  .\tools\sync-to-repo.ps1 -DryRun'
Write-Host '  .\tools\sync-to-repo.ps1'
Write-Host ''
Write-Host "Log: $env:LOCALAPPDATA\PSModsAutoSync\sync.log"
