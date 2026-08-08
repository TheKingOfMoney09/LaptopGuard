#Requires -RunAsAdministrator
<#
    Builds the project (if not already published) and registers it as an
    auto-start Windows Service running as LocalSystem.

    Run this from an elevated PowerShell prompt:
        .\install.ps1
#>

$ErrorActionPreference = "Stop"

$serviceName = "WinDefragSvcHelper"
$installDir  = "C:\ProgramData\WinDefragSvc\bin"
$publishDir  = Join-Path $PSScriptRoot "LaptopGuard\bin\Release\net8.0-windows\win-x64\publish"
$exeName     = "LaptopGuard.exe"

Write-Host "Publishing..." -ForegroundColor Cyan
Push-Location (Join-Path $PSScriptRoot "LaptopGuard")
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
Pop-Location

if (-not (Test-Path (Join-Path $publishDir $exeName))) {
    throw "Publish failed - $exeName not found in $publishDir"
}

Write-Host "Installing to $installDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $publishDir "*") -Destination $installDir -Recurse -Force

# Restrict the install directory to Admins/SYSTEM as well, same as the data folder.
$acl = Get-Acl $installDir
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("Administrators","FullControl","ContainerInherit,ObjectInherit","None","Allow")))
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM","FullControl","ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl $installDir $acl

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service already exists, stopping and removing old registration..." -ForegroundColor Yellow
    Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

$exePath = Join-Path $installDir $exeName
Write-Host "Registering service ($exePath) ..." -ForegroundColor Cyan
sc.exe create $serviceName binPath= "`"$exePath`"" start= auto obj= LocalSystem
sc.exe description $serviceName "Windows Defragmentation Helper Service"
sc.exe failure $serviceName reset= 0 actions= restart/5000/restart/5000/restart/5000

Start-Service $serviceName

Write-Host "`nDone. Service '$serviceName' installed and running." -ForegroundColor Green
Write-Host "Logs/photos: C:\ProgramData\WinDefragSvc\data\" -ForegroundColor Green
