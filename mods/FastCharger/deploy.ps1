# Copies the repo build of Fast Charger into the game once it is closed.
$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\FastCharger'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\FastCharger'

$deadline = (Get-Date).AddMinutes(120)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) {
    Write-Output 'game still running - not deployed'
    exit 1
}

New-Item -ItemType Directory $dst -Force | Out-Null
New-Item -ItemType Directory (Join-Path $dst 'Textures\Items') -Force | Out-Null
Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\FastCharger.dll') (Join-Path $dst 'FastCharger.dll') -Force
Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
Copy-Item (Join-Path $src 'README.md') (Join-Path $dst 'README.md') -Force
Copy-Item (Join-Path $src 'Textures\Items\*.png') (Join-Path $dst 'Textures\Items') -Force
foreach ($image in Get-ChildItem $src -File -Filter 'preview*') { Copy-Item $image.FullName $dst -Force }

Write-Output ('deployed: ' + (Get-Item (Join-Path $dst 'FastCharger.dll')).Length + ' bytes ' + (Get-FileHash (Join-Path $dst 'FastCharger.dll')).Hash.Substring(0,16))

