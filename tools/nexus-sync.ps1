<#
.SYNOPSIS
	Brings Nexus up to date with the repository, on top of the Workshop and repo sync.

.DESCRIPTION
	For every mod in tools\nexus-mods.json it asks the v3 API what the page and its MAIN file version are. A mod is
	behind when either differs from <ModVersion> in the repo manifest. For each behind mod it builds, rebuilds the
	dist zip, and pushes the new version through tools\nexus_push.py, which also sets the page version, archives the
	previous file and adds the changelog.

	A mod without a changelog in dist is skipped and logged: the changelog text is public, so it is never invented.

	Chained from tools\sync-to-repo.ps1, so the existing watcher covers Workshop, repo and Nexus in one pass.

.PARAMETER DryRun
	Report what would happen without building, zipping or pushing.

.PARAMETER Only
	One repo folder, display name or Nexus mod id.

.PARAMETER SkipBuild
	Zip what is already in bin\Release instead of rebuilding first.
#>
[CmdletBinding()]
param(
	[string]$Only,
	[switch]$DryRun,
	[switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$repo = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods'
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { $python = 'python' }
$pushScript = 'C:\Users\RWJVH\Documents\Default Project\PersonalModFixes\tools\nexus_push.py'
$zipTool = 'C:\Users\RWJVH\Documents\Default Project\PersonalModFixes\tools\make-nexus-zip.ps1'
$configPath = Join-Path $repo 'tools\nexus-mods.json'

$logDir = Join-Path $env:LOCALAPPDATA 'PSModsAutoSync'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir 'nexus.log'
if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 1MB) {
	Move-Item $logFile ($logFile + '.old') -Force
}

function Write-Log([string]$message) {
	$line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $message
	Add-Content -Path $logFile -Value $line
	Write-Host $line
}

function Get-ChangelogPrefix([string]$repoFolder) {
	$config = Get-Content $configPath -Raw | ConvertFrom-Json
	$entry = $config.mods | Where-Object { $_.repo -eq $repoFolder } | Select-Object -First 1
	if ($entry -and $entry.changelog_prefix) { return $entry.changelog_prefix }
	return 'changelog-' + $repoFolder
}

# One run at a time.
$mutex = New-Object System.Threading.Mutex($false, 'Global\PSModsNexusSync')
if (-not $mutex.WaitOne(0)) {
	Write-Log 'Another Nexus sync is running, skipping.'
	return
}

try {
	$pushArgs = @($pushScript, '--json')
	if ($Only) { $pushArgs += @('--only', $Only) }
	$json = & $python @pushArgs
	if ($LASTEXITCODE -ne 0) { throw "nexus_push.py --json failed ($LASTEXITCODE)" }
	$plan = $json | ConvertFrom-Json
	$behind = @($plan | Where-Object { $_.behind })

	Write-Log ("checked {0} mod(s), {1} behind" -f @($plan).Count, $behind.Count)
	foreach ($row in $behind) {
		Write-Log ("  behind: {0} repo {1}, Nexus page {2}, main file {3}" -f $row.label, $row.version, $row.page, $row.main)
	}

	if ($behind.Count -eq 0) { return }
	if ($DryRun) {
		Write-Log 'Dry run: nothing built, zipped or pushed.'
		foreach ($row in $behind) { Write-Log ("  would push " + $row.label + " " + $row.version) }
		return
	}

	$failures = 0
	foreach ($row in $behind) {
		$folder = Join-Path $repo ('mods\' + $row.repo)
		$changelog = Join-Path $repo ('dist\' + (Get-ChangelogPrefix $row.repo) + '-' + $row.version + '.txt')
		if (-not (Test-Path $changelog)) {
			Write-Log ("  SKIP {0}: no changelog at {1}; write it and the next run will push" -f $row.label, (Split-Path $changelog -Leaf))
			$failures++
			continue
		}
		try {
			if (-not $SkipBuild) {
				$project = Get-ChildItem $folder -Filter *.csproj | Select-Object -First 1
				if (-not $project) { throw 'no csproj' }
				Write-Log ("  building " + $row.label)
				& dotnet build $project.FullName -c Release --nologo 2>&1 | ForEach-Object { Write-Log ("    " + $_) }
				if ($LASTEXITCODE -ne 0) { throw 'build failed' }
			}
			Write-Log ("  packaging " + $row.label)
			& $zipTool -ModDir $folder -OutDir (Join-Path $repo 'dist') | Out-Null
			Write-Log ("  pushing " + $row.label + " " + $row.version)
			& $python $pushScript --only $row.repo 2>&1 | ForEach-Object { Write-Log ("    " + $_) }
			if ($LASTEXITCODE -ne 0) { throw 'push failed' }
		}
		catch {
			Write-Log ("  FAILED {0}: {1}" -f $row.label, $_.Exception.Message)
			$failures++
		}
	}
	Write-Log ("done, {0} failure(s)" -f $failures)
}
finally {
	$mutex.ReleaseMutex()
}
