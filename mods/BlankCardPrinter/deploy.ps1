# Copies the repo build of Blank Card Printer into the game once it is closed.
$src = 'C:\Users\RWJVH\Documents\Default Project\ProbablyStolenMods\mods\BlankCardPrinter'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\BlankCardPrinter'

$deadline = (Get-Date).AddMinutes(180)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) {
    Write-Output 'game still running - not deployed'
    exit 1
}

New-Item -ItemType Directory $dst -Force | Out-Null
New-Item -ItemType Directory (Join-Path $dst 'Textures\Items') -Force | Out-Null
Copy-Item (Join-Path $src 'bin\Release\netstandard2.1\BlankCardPrinter.dll') (Join-Path $dst 'BlankCardPrinter.dll') -Force
Copy-Item (Join-Path $src 'manifest.xml') (Join-Path $dst 'manifest.xml') -Force
Copy-Item (Join-Path $src 'README.md') (Join-Path $dst 'README.md') -Force
Copy-Item (Join-Path $src 'Textures\Items\*.png') (Join-Path $dst 'Textures\Items') -Force

Write-Output ('deployed: ' + (Get-Item (Join-Path $dst 'BlankCardPrinter.dll')).Length + ' bytes ' + (Get-FileHash (Join-Path $dst 'BlankCardPrinter.dll')).Hash.Substring(0,16))
