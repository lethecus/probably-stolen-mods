$local = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Mods\CalendarTV'
$deadline = (Get-Date).AddMinutes(240)
while ((Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 3
}
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) { 'game still running - local copy kept'; exit 1 }
if (Test-Path $local) {
  Remove-Item $local -Recurse -Force
  Write-Output 'local test copy removed; Workshop build 1.0.7 is now the only copy'
} else {
  Write-Output 'local copy already gone'
}
