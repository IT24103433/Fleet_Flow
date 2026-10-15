<#
.SYNOPSIS
    Orchestrated multi-service Azure deployment script for FleetFlow.
.DESCRIPTION
    Phase 1: Build & Deploy IdentityService -> Smoke Probe /health/ready
    Phase 2: Build & Deploy FleetService, RentalService, NotificationService -> Smoke Probe
    Phase 3: Build & Deploy React/Vite Frontend -> Smoke Probe
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup = "rg-fleetflow",
    [switch]$SkipFrontend,
    [switch]$SkipBackend
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

function Write-Step([string]$msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "[SUCCESS] $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
    Write-Host "[WARN] $msg" -ForegroundColor Yellow
}

function Test-ServiceReady([string]$name, [string]$probeUrl, [int]$maxRetries = 25) {
    Write-Host "Probing $name readiness at $probeUrl..." -ForegroundColor Yellow
    for ($i = 1; $i -le $maxRetries; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $probeUrl -UseBasicParsing -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                Write-Ok "$name is HEALTHY and READY (HTTP 200)!"
                return $true
            }
            Write-Host "  Attempt $i : Received HTTP $($response.StatusCode), retrying in 5s..." -ForegroundColor DarkGray
        } catch {
            Write-Host "  Attempt $i : $($_.Exception.Message), retrying in 5s..." -ForegroundColor DarkGray
        }
        Start-Sleep -Seconds 5
    }
    throw "$name failed health check probe after $($maxRetries * 5) seconds."
}

$RootDir = (Get-Item $PSScriptRoot).Parent.FullName
$ArtifactsDir = Join-Path $RootDir "artifacts\publish"
if (Test-Path $ArtifactsDir) {
    Remove-Item $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

# -------------------------------------------------------------------------
# PHASE 1: IDENTITY SERVICE
# -------------------------------------------------------------------------
if (-not $SkipBackend) {
    Write-Step "Phase 1 : Publishing and Deploying IdentityService"
    $identityProj = Join-Path $RootDir "src\backend\services\IdentityService\IdentityService.Api\IdentityService.Api.csproj"
    $identityOut = Join-Path $ArtifactsDir "identity-app"
    $identityZip = Join-Path $ArtifactsDir "identity-app.zip"

    Write-Host "Compiling self-contained Linux binary..." -ForegroundColor Gray
    & dotnet publish $identityProj -c Release -r linux-x64 --self-contained true -o $identityOut
    if ($LASTEXITCODE -ne 0) { throw "IdentityService publish failed." }

    Write-Host "Packaging zip..." -ForegroundColor Gray
    Compress-Archive -Path "$identityOut\*" -DestinationPath $identityZip -Force

    Write-Host "Deploying to Azure Web App 'fleetflow-identity-api'..." -ForegroundColor Cyan
    & az webapp deploy --resource-group $ResourceGroup --name "fleetflow-identity-api" --src-path $identityZip --type zip --output none
    if ($LASTEXITCODE -ne 0) { throw "IdentityService deployment failed." }

    Test-ServiceReady -name "IdentityService" -probeUrl "https://fleetflow-identity-api.azurewebsites.net/health/ready"

    # -------------------------------------------------------------------------
    # PHASE 2: FLEET, RENTAL, NOTIFICATION SERVICES
    # -------------------------------------------------------------------------
    Write-Step "Phase 2A : Publishing and Deploying FleetService"
    $fleetProj = Join-Path $RootDir "src\backend\services\FleetService\FleetService.Api\FleetService.Api.csproj"
    $fleetOut = Join-Path $ArtifactsDir "fleet-app"
    $fleetZip = Join-Path $ArtifactsDir "fleet-app.zip"
    & dotnet publish $fleetProj -c Release -r linux-x64 --self-contained true -o $fleetOut
    if ($LASTEXITCODE -ne 0) { throw "FleetService publish failed." }
    Compress-Archive -Path "$fleetOut\*" -DestinationPath $fleetZip -Force
    & az webapp deploy --resource-group $ResourceGroup --name "fleetflow-fleet-api" --src-path $fleetZip --type zip --output none
    if ($LASTEXITCODE -ne 0) { throw "FleetService deployment failed." }

    Write-Step "Phase 2B : Publishing and Deploying RentalService"
    $rentalProj = Join-Path $RootDir "src\backend\services\RentalService\RentalService.Api\RentalService.Api.csproj"
    $rentalOut = Join-Path $ArtifactsDir "rental-app"
    $rentalZip = Join-Path $ArtifactsDir "rental-app.zip"
    & dotnet publish $rentalProj -c Release -r linux-x64 --self-contained true -o $rentalOut
    if ($LASTEXITCODE -ne 0) { throw "RentalService publish failed." }
    Compress-Archive -Path "$rentalOut\*" -DestinationPath $rentalZip -Force
    & az webapp deploy --resource-group $ResourceGroup --name "fleetflow-rental-api" --src-path $rentalZip --type zip --output none
    if ($LASTEXITCODE -ne 0) { throw "RentalService deployment failed." }

    Write-Step "Phase 2C : Publishing and Deploying NotificationService"
    $notifProj = Join-Path $RootDir "src\backend\services\NotificationService\NotificationService.Api\NotificationService.Api.csproj"
    $notifOut = Join-Path $ArtifactsDir "notification-app"
    $notifZip = Join-Path $ArtifactsDir "notification-app.zip"
    & dotnet publish $notifProj -c Release -r linux-x64 --self-contained true -o $notifOut
    if ($LASTEXITCODE -ne 0) { throw "NotificationService publish failed." }
    Compress-Archive -Path "$notifOut\*" -DestinationPath $notifZip -Force
    & az webapp deploy --resource-group $ResourceGroup --name "fleetflow-notification-api" --src-path $notifZip --type zip --output none
    if ($LASTEXITCODE -ne 0) { throw "NotificationService deployment failed." }

    Test-ServiceReady -name "FleetService" -probeUrl "https://fleetflow-fleet-api.azurewebsites.net/health/ready"
    Test-ServiceReady -name "RentalService" -probeUrl "https://fleetflow-rental-api.azurewebsites.net/health/ready"
    Test-ServiceReady -name "NotificationService" -probeUrl "https://fleetflow-notification-api.azurewebsites.net/health/ready"
}

# -------------------------------------------------------------------------
# PHASE 3: FRONTEND
# -------------------------------------------------------------------------
if (-not $SkipFrontend) {
    Write-Step "Phase 3 : Compiling and Deploying React Frontend"
    $frontendDir = Join-Path $RootDir "src\frontend"
    Push-Location $frontendDir
    try {
        Write-Host "Compiling Vite production bundle with live API endpoints..." -ForegroundColor Gray
        $env:VITE_IDENTITY_API_URL = "https://fleetflow-identity-api.azurewebsites.net"
        $env:VITE_FLEET_API_URL = "https://fleetflow-fleet-api.azurewebsites.net"
        $env:VITE_RENTAL_API_URL = "https://fleetflow-rental-api.azurewebsites.net"
        $env:VITE_NOTIFICATION_API_URL = "https://fleetflow-notification-api.azurewebsites.net"

        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }

        Copy-Item -Path "server.js" -Destination "dist\server.js" -Force
        Copy-Item -Path "package.deploy.json" -Destination "dist\package.json" -Force

        $frontendZip = Join-Path $ArtifactsDir "frontend-app.zip"
        Compress-Archive -Path "dist\*" -DestinationPath $frontendZip -Force
    }
    finally {
        Pop-Location
    }

    Write-Host "Deploying to Azure Web App 'fleetflow-frontend'..." -ForegroundColor Cyan
    & az webapp deploy --resource-group $ResourceGroup --name "fleetflow-frontend" --src-path $frontendZip --type zip --output none
    if ($LASTEXITCODE -ne 0) { throw "Frontend deployment failed." }

    Test-ServiceReady -name "Frontend" -probeUrl "https://fleetflow-frontend.azurewebsites.net"
}

$Duration = (Get-Date) - $StartTime
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " FLEETFLOW MULTI-SERVICE RELEASE COMPLETED IN $($Duration.ToString('mm\:ss'))" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Ok "Identity Service     : https://fleetflow-identity-api.azurewebsites.net"
Write-Ok "Fleet Service        : https://fleetflow-fleet-api.azurewebsites.net"
Write-Ok "Rental Service       : https://fleetflow-rental-api.azurewebsites.net"
Write-Ok "Notification Service : https://fleetflow-notification-api.azurewebsites.net"
Write-Ok "Frontend Web App     : https://fleetflow-frontend.azurewebsites.net"
