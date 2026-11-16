$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\CalendarTV'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\CalendarTV'
$deadline = (Get-Date).AddMinutes(240)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 3
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) { 'game still running - not deployed'; exit 1 }

$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) {
  try {
    Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\CalendarTV.dll') (Join-Path $dst 'CalendarTV.dll') -Force -ErrorAction Stop
    $ok = $true
  } catch {
    Start-Sleep -Seconds 2
  }
}
if (-not $ok) { 'could not replace the DLL'; exit 1 }
Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
$dllHash = (Get-FileHash (Join-Path $dst 'CalendarTV.dll')).Hash.Substring(0,16)
$ver = ([regex]::Match((Get-Content (Join-Path $dst 'manifest.xml') -Raw), '<ModVersion>([^<]*)</ModVersion>')).Groups[1].Value
Write-Output ('deployed ' + (Get-Item (Join-Path $dst 'CalendarTV.dll')).Length + ' bytes ' + $dllHash + '  manifest ' + $ver)
