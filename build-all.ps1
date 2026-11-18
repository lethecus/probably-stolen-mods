# Builds every mod in .\mods, optionally deploying them into the game's Mods folder.
#
#   .\build-all.ps1
#   .\build-all.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
#   .\build-all.ps1 -Deploy
#
param(
	[string]$GameDir,
	[string]$ModsDir,
	[switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $GameDir) {
	$props = Join-Path $root 'Directory.Build.props'
	if (Test-Path $props) {
		[xml]$xml = Get-Content $props -Raw
		$GameDir = $xml.Project.PropertyGroup.GameDir
	}
}
if ($Deploy -and -not $ModsDir) {
	if (-not $GameDir) {
		throw 'Set -GameDir or -ModsDir so the built mods know where to go.'
	}
	$ModsDir = Join-Path $GameDir 'Mods'
}

$buildArgs = @('-c', 'Release', '--nologo')
if ($GameDir) {
	$buildArgs += "-p:GameDir=$GameDir"
}

$projects = Get-ChildItem (Join-Path $root 'mods') -Recurse -Filter *.csproj | Sort-Object FullName
if ($projects.Count -eq 0) {
	throw 'No .csproj files found under .\mods'
}

$failed = @()
foreach ($project in $projects) {
	$name = $project.BaseName
	Write-Host ''
	Write-Host "=== $name ===" -ForegroundColor Cyan
	& dotnet build $project.FullName @buildArgs
	if ($LASTEXITCODE -ne 0) {
		$failed += $name
		Write-Host "Build failed: $name" -ForegroundColor Red
		continue
	}

	if (-not $Deploy) {
		continue
	}

	$modSource = $project.Directory.FullName
	$manifestPath = Join-Path $modSource 'manifest.xml'
	if (-not (Test-Path $manifestPath)) {
		Write-Warning "$name has no manifest.xml, skipped deployment"
		continue
	}
	[xml]$manifest = Get-Content $manifestPath -Raw
	# Two manifest shapes are in use: <Manifest> and <Mod>. Reading only .Manifest silently skipped the DLL for
	# every mod using <Mod> (Fast Charger did not deploy its assembly at all until 18 Nov 2026).
	$manifestRoot = if ($manifest.Manifest) { $manifest.Manifest } else { $manifest.Mod }
	$target = Join-Path $ModsDir (Split-Path $modSource -Leaf)
	New-Item -ItemType Directory -Force -Path $target | Out-Null

	Copy-Item $manifestPath (Join-Path $target 'manifest.xml') -Force

	$binDir = Join-Path $modSource 'bin\Release\netstandard2.1'
	foreach ($assembly in $manifestRoot.Assemblies.Assembly) {
		$dll = Join-Path $binDir $assembly
		if (-not (Test-Path $dll)) {
			$dll = Get-ChildItem (Join-Path $modSource 'bin\Release') -Recurse -Filter $assembly | Select-Object -First 1 -ExpandProperty FullName
		}
		if (Test-Path $dll) {
			Copy-Item $dll $target -Force
		} else {
			Write-Warning "$name : could not find $assembly"
		}
	}

	# Workshop uploads are built from the game's Mods folder, so the preview image has to be deployed too or the
	# uploaded item loses its thumbnail.
	foreach ($image in Get-ChildItem $modSource -File -Filter 'preview*') {
		Copy-Item $image.FullName (Join-Path $target $image.Name) -Force
	}
	$readme = Join-Path $modSource 'README.md'
	if (Test-Path $readme) {
		Copy-Item $readme (Join-Path $target 'README.md') -Force
	}

	# Assets the loader reads from next to the DLL (Textures, Localization, Sounds, ...).
	foreach ($folder in 'Textures', 'Localization', 'Sounds', 'Assets') {
		$assetDir = Join-Path $modSource $folder
		if (Test-Path $assetDir) {
			Copy-Item $assetDir $target -Recurse -Force
		}
	}
	Write-Host "Deployed to $target" -ForegroundColor Green
}

Write-Host ''
if ($failed.Count -gt 0) {
	Write-Host ("Failed: " + ($failed -join ', ')) -ForegroundColor Red
	exit 1
}
Write-Host 'All mods built successfully.' -ForegroundColor Green
