#Requires -RunAsAdministrator
$serviceName = "WinDefragSvcHelper"

Get-Process -Name "LaptopGuard" -ErrorAction SilentlyContinue | Stop-Process -Force

Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
sc.exe delete $serviceName

Write-Host "Service removed. Logs/photos left in place at C:\ProgramData\WinDefragSvc\data\"
Write-Host "Delete that folder manually if you also want to wipe the captured data."
