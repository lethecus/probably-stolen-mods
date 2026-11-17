$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\SaveTheNewspaper'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\SaveTheNewspaper'
$deadline = (Get-Date).AddMinutes(240)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 3
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) { 'game still running - not deployed'; exit 1 }
New-Item -ItemType Directory $dst -Force | Out-Null
$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) {
  try {
    Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\SaveTheNewspaper.dll') (Join-Path $dst 'SaveTheNewspaper.dll') -Force -ErrorAction Stop
    $ok = $true
  } catch { Start-Sleep -Seconds 2 }
}
if (-not $ok) { 'could not replace the DLL'; exit 1 }
Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
Write-Output ('deployed ' + (Get-Item (Join-Path $dst 'SaveTheNewspaper.dll')).Length + ' bytes ' + (Get-FileHash (Join-Path $dst 'SaveTheNewspaper.dll')).Hash.Substring(0,16))
