# Builds the React app and publishes the API (with the UI inside) to ..\publish.
# Creates publish\appsettings.Production.json on first run (DB connection + a new random JWT key).
# Does not need Administrator rights. The service must be stopped first if it is running (update.ps1 does that).

$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'publish'
$utf8    = New-Object System.Text.UTF8Encoding($false)

Write-Host '1/3 Building the web app...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'client')
try {
    if (-not (Test-Path 'node_modules')) { npm install; if ($LASTEXITCODE) { throw 'npm install failed' } }
    npm run build:api; if ($LASTEXITCODE) { throw 'Web app build failed' }
} finally { Pop-Location }

Write-Host '2/3 Publishing the server...' -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\CabinBooking.Api\CabinBooking.Api.csproj') -c Release -o $publish --nologo
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

# The dev-only settings file must not leak into the service (it would be ignored anyway in Production).
Remove-Item (Join-Path $publish 'appsettings.Development.json') -ErrorAction SilentlyContinue

$prod = Join-Path $publish 'appsettings.Production.json'
if (Test-Path $prod) {
    Write-Host '3/3 Keeping existing appsettings.Production.json' -ForegroundColor Cyan
} else {
    Write-Host '3/3 Creating appsettings.Production.json...' -ForegroundColor Cyan
    $devPath = Join-Path $root 'src\CabinBooking.Api\appsettings.Development.json'
    $conn = $env:ConnectionStrings__CabinBooking
    if (-not $conn -and (Test-Path $devPath)) {
        $dev = Get-Content $devPath -Raw | ConvertFrom-Json
        $conn = $dev.ConnectionStrings.CabinBooking
    }
    if (-not $conn) {
        $conn = 'Server=localhost;Database=CabinBookingDb;Trusted_Connection=True;TrustServerCertificate=True;'
    }
    $bytes = New-Object byte[] 48
    (New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
    $settings = [ordered]@{
        Urls              = 'http://0.0.0.0:5080'
        ConnectionStrings = @{ CabinBooking = $conn }
        Jwt               = @{ SigningKey = [Convert]::ToBase64String($bytes) }
        Logging           = @{ LogLevel = @{ Default = 'Warning'; 'Microsoft.Hosting.Lifetime' = 'Information' } }
    }
    [IO.File]::WriteAllText($prod, ($settings | ConvertTo-Json -Depth 5), $utf8)
}

Write-Host "Published to $publish" -ForegroundColor Green
