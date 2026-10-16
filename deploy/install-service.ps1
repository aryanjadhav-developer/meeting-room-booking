# Installs Cabin Booking as a Windows service that starts automatically with Windows.
# Run once in PowerShell **as Administrator**:
#   powershell -ExecutionPolicy Bypass -File ".\deploy\install-service.ps1"

$ErrorActionPreference = 'Stop'
$serviceName = 'CabinBooking'
$port        = 5080
$root        = Split-Path -Parent $PSScriptRoot
$exe         = Join-Path $root 'publish\CabinBooking.Api.exe'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Please run this script in PowerShell opened with "Run as administrator".' }

if (-not (Test-Path $exe)) {
    Write-Host 'App not published yet, publishing now...' -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'publish.ps1')
}

# Anything still holding the port (e.g. a "dotnet run" window) would stop the service from starting.
$busy = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
if ($busy) { throw "Port $port is already in use (process id $($busy[0].OwningProcess)). Close the other app first." }

$existing = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Removing the previous installation...' -ForegroundColor Yellow
    if ($existing.Status -ne 'Stopped') { Stop-Service $serviceName -Force }
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host 'Installing the service...' -ForegroundColor Cyan
New-Service -Name $serviceName -DisplayName 'Cabin Booking' `
    -Description 'Office cabin booking web app (http://<this-pc>:5080).' `
    -BinaryPathName "`"$exe`"" -StartupType Automatic | Out-Null

# Start after the local SQL Server at boot, so the first database connection doesn't fail.
if (Get-Service MSSQLSERVER -ErrorAction SilentlyContinue) { sc.exe config $serviceName depend= MSSQLSERVER | Out-Null }

# Start automatically after a crash: retry after 5s, 5s, then 30s; reset the counter daily.
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null

if (-not (Get-NetFirewallRule -DisplayName 'Cabin Booking*' -ErrorAction SilentlyContinue)) {
    Write-Host "Allowing port $port through Windows Firewall..." -ForegroundColor Cyan
    New-NetFirewallRule -DisplayName "Cabin Booking (port $port)" -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow -Profile Any | Out-Null
}

Write-Host 'Starting...' -ForegroundColor Cyan
Start-Service $serviceName

$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) {
    Start-Sleep -Seconds 1
    try { $ok = (Invoke-WebRequest "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { }
}

$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object -First 1).IPAddress
if ($ok) {
    Write-Host ''
    Write-Host 'Cabin Booking is running as a Windows service.' -ForegroundColor Green
    Write-Host "  Open: http://${ip}:$port" -ForegroundColor Green
    Write-Host '  It starts automatically whenever this PC starts.'
} else {
    Write-Host 'The service was installed but did not respond yet. Check: Event Viewer > Windows Logs > Application (source: CabinBooking).' -ForegroundColor Red
    exit 1
}
