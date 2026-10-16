# Publishes the latest code and restarts the service. Run in PowerShell **as Administrator**:
#   powershell -ExecutionPolicy Bypass -File ".\deploy\update.ps1"
# Database changes (migrations) are applied automatically when the service starts.

$ErrorActionPreference = 'Stop'
$serviceName = 'CabinBooking'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Please run this script in PowerShell opened with "Run as administrator".' }
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) { throw 'Service not installed. Run install-service.ps1 first.' }

Write-Host 'Stopping the service...' -ForegroundColor Cyan
Stop-Service $serviceName -Force

try {
    & (Join-Path $PSScriptRoot 'publish.ps1')
} finally {
    Write-Host 'Starting the service...' -ForegroundColor Cyan
    Start-Service $serviceName
}

Start-Sleep -Seconds 5
try {
    $null = Invoke-WebRequest 'http://localhost:5080/health' -UseBasicParsing -TimeoutSec 10
    Write-Host 'Update complete, Cabin Booking is running.' -ForegroundColor Green
} catch {
    Write-Host 'Service restarted but is not responding yet. Check Event Viewer > Application (source: CabinBooking).' -ForegroundColor Red
}
