# Infrastructure Deployment & Secret Helper Script for FleetFlow
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory=$false)]
    [string]$Location = "koreacentral",

    [Parameter(Mandatory=$true)]
    [SecureString]$PostgresPassword
)

$azCmd = "az"
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    $azCmd = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
}

$BicepFile = Join-Path $PSScriptRoot "main.bicep"

Write-Host "=== 1. Checking Azure Login ===" -ForegroundColor Cyan
$account = & $azCmd account show 2>$null
if (-not $account) {
    Write-Host "Please log in to your Azure account..." -ForegroundColor Yellow
    & $azCmd login --output table
}

Write-Host "`n=== 2. Provisioning Infrastructure via Bicep in $Location ===" -ForegroundColor Cyan
$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($PostgresPassword))

& $azCmd deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $BicepFile `
    --parameters location=$Location administratorLoginPassword=$plainPassword `
    --output table

Write-Host "`n=== 3. Retrieving GitHub Deployment Secrets ===" -ForegroundColor Cyan

$identityAppName = "fleetflow-identity-api"
$fleetAppName = "fleetflow-fleet-api"
$frontendAppName = "fleetflow-frontend"

Write-Host "Fetching Publish Profile for Identity Service App ($identityAppName)..." -ForegroundColor Yellow
$identityPublishProfile = & $azCmd webapp deployment list-publishing-profiles --name $identityAppName --resource-group $ResourceGroupName --xml 2>$null

Write-Host "Fetching Publish Profile for Fleet Service App ($fleetAppName)..." -ForegroundColor Yellow
$fleetPublishProfile = & $azCmd webapp deployment list-publishing-profiles --name $fleetAppName --resource-group $ResourceGroupName --xml 2>$null

Write-Host "Fetching Publish Profile for Frontend Web App ($frontendAppName)..." -ForegroundColor Yellow
$frontendPublishProfile = & $azCmd webapp deployment list-publishing-profiles --name $frontendAppName --resource-group $ResourceGroupName --xml 2>$null

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host " COPY THESE SECRETS TO GITHUB REPOSITORY SECRETS " -ForegroundColor Green
Write-Host " (https://github.com/IT24103433/Fleet_Flow/settings/secrets/actions)" -ForegroundColor Green
Write-Host "========================================================`n" -ForegroundColor Green

Write-Host "1. AZURE_IDENTITY_SERVICE_APP_NAME = $identityAppName"
Write-Host "2. AZURE_FLEET_SERVICE_APP_NAME = $fleetAppName"
Write-Host "3. AZURE_FRONTEND_APP_NAME = $frontendAppName`n"

Write-Host "--- AZURE_IDENTITY_SERVICE_PUBLISH_PROFILE ---" -ForegroundColor Yellow
Write-Host $identityPublishProfile

Write-Host "`n--- AZURE_FLEET_SERVICE_PUBLISH_PROFILE ---" -ForegroundColor Yellow
Write-Host $fleetPublishProfile

Write-Host "`n--- AZURE_FRONTEND_PUBLISH_PROFILE ---" -ForegroundColor Yellow
Write-Host $frontendPublishProfile
