$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\HiddenWallStash'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\HiddenWallStash'
$deadline = (Get-Date).AddMinutes(240)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 3
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) { 'game still running - not deployed'; exit 1 }

New-Item -ItemType Directory $dst -Force | Out-Null
New-Item -ItemType Directory (Join-Path $dst 'Textures') -Force | Out-Null

$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) {
  try {
    Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\HiddenWallStash.dll') (Join-Path $dst 'HiddenWallStash.dll') -Force -ErrorAction Stop
    $ok = $true
  } catch { Start-Sleep -Seconds 2 }
}
if (-not $ok) { 'could not replace the DLL'; exit 1 }

Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
Copy-Item (Join-Path $src 'Textures\grate.png') (Join-Path $dst 'Textures\grate.png') -Force
Write-Output ('deployed ' + (Get-Item (Join-Path $dst 'HiddenWallStash.dll')).Length + ' bytes ' + (Get-FileHash (Join-Path $dst 'HiddenWallStash.dll')).Hash.Substring(0,16))
