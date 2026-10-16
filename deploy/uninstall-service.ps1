# Stops and removes the Cabin Booking Windows service and its firewall rule.
# The database and the publish folder are left untouched.
# Run in PowerShell **as Administrator**:
#   powershell -ExecutionPolicy Bypass -File ".\deploy\uninstall-service.ps1"

$ErrorActionPreference = 'Stop'
$serviceName = 'CabinBooking'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Please run this script in PowerShell opened with "Run as administrator".' }

$svc = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') { Stop-Service $serviceName -Force }
    sc.exe delete $serviceName | Out-Null
    Write-Host 'Service removed.' -ForegroundColor Green
} else {
    Write-Host 'Service was not installed.'
}

Get-NetFirewallRule -DisplayName 'Cabin Booking*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Write-Host 'Firewall rule removed (if it existed).' -ForegroundColor Green
