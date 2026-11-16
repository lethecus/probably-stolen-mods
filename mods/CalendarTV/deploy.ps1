# Copies the repo build of Calendar TV into the game once it is closed.
$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\CalendarTV'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\CalendarTV'

$deadline = (Get-Date).AddMinutes(180)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) {
    Write-Output 'game still running - not deployed'
    exit 1
}

New-Item -ItemType Directory $dst -Force | Out-Null
Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\CalendarTV.dll') (Join-Path $dst 'CalendarTV.dll') -Force
Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
if (Test-Path (Join-Path $src 'README.md')) { Copy-Item (Join-Path $src 'README.md') (Join-Path $dst 'README.md') -Force }
if (Test-Path (Join-Path $src 'preview.png')) { Copy-Item (Join-Path $src 'preview.png') (Join-Path $dst 'preview.png') -Force }

Write-Output ('deployed: ' + (Get-Item (Join-Path $dst 'CalendarTV.dll')).Length + ' bytes ' + (Get-FileHash (Join-Path $dst 'CalendarTV.dll')).Hash.Substring(0,16))
