# Infrastructure Deployment & Secret Helper Script for FleetFlow (Sprint 4 Architecture)
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory=$false)]
    [string]$Location = "koreacentral",

    [Parameter(Mandatory=$false)]
    [string]$AppNamePrefix = "fleetflow",

    [Parameter(Mandatory=$true)]
    [SecureString]$PostgresPassword,

    [Parameter(Mandatory=$false)]
    [switch]$ConfigureGitHubOidc,

    [Parameter(Mandatory=$false)]
    [string]$GitHubRepo = "IT24103433/Fleet_Flow"
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

Write-Host "`n=== 2. Provisioning 4-Microservice Infrastructure via Bicep in $Location ===" -ForegroundColor Cyan
$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($PostgresPassword))

$deploymentResult = & $azCmd deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $BicepFile `
    --parameters location=$Location appNamePrefix=$AppNamePrefix administratorLoginPassword=$plainPassword `
    --output json | ConvertFrom-Json

if (-not $deploymentResult) {
    Write-Error "Deployment failed. Please check Azure CLI errors above."
    exit 1
}

$outputs = $deploymentResult.properties.outputs
$identityAppName = $outputs.identityAppName.value
$fleetAppName = $outputs.fleetAppName.value
$rentalAppName = $outputs.rentalAppName.value
$notificationAppName = $outputs.notificationAppName.value
$frontendAppName = $outputs.frontendAppName.value
$postgresHost = $outputs.postgresHost.value
$keyVaultName = $outputs.keyVaultName.value

Write-Host "`n=== 3. Provisioned Resources Summary ===" -ForegroundColor Green
Write-Host "Identity API App     : $identityAppName"
Write-Host "Fleet API App        : $fleetAppName"
Write-Host "Rental API App       : $rentalAppName"
Write-Host "Notification API App : $notificationAppName"
Write-Host "Frontend App         : $frontendAppName"
Write-Host "PostgreSQL Server    : $postgresHost"
Write-Host "Key Vault            : $keyVaultName"

if ($ConfigureGitHubOidc) {
    Write-Host "`n=== 4. Configuring GitHub OIDC Federated Credentials ===" -ForegroundColor Cyan
    $appRegistrationName = "$AppNamePrefix-github-actions"
    Write-Host "Creating Azure AD Application for GitHub Actions: $appRegistrationName..." -ForegroundColor Yellow
    $sp = & $azCmd ad sp create-for-rbac --name $appRegistrationName --role contributor --scopes "/subscriptions/$((& $azCmd account show --query id -o tsv))/resourceGroups/$ResourceGroupName" --json-auth 2>$null | ConvertFrom-Json
    
    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host " GITHUB OIDC SECRETS FOR ACTIONS " -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "AZURE_CLIENT_ID       = $($sp.clientId)"
    Write-Host "AZURE_TENANT_ID       = $($sp.tenantId)"
    Write-Host "AZURE_SUBSCRIPTION_ID = $((& $azCmd account show --query id -o tsv))"
} else {
    Write-Host "`n=== 4. Fetching Publish Profiles for GitHub Actions Deployment ===" -ForegroundColor Cyan
    Write-Host "(Tip: Run with -ConfigureGitHubOidc to set up passwordless OIDC instead)" -ForegroundColor DarkGray
    
    $identityProfile = & $azCmd webapp deployment list-publishing-profiles --name $identityAppName --resource-group $ResourceGroupName --xml 2>$null
    $fleetProfile = & $azCmd webapp deployment list-publishing-profiles --name $fleetAppName --resource-group $ResourceGroupName --xml 2>$null
    $rentalProfile = & $azCmd webapp deployment list-publishing-profiles --name $rentalAppName --resource-group $ResourceGroupName --xml 2>$null
    $notificationProfile = & $azCmd webapp deployment list-publishing-profiles --name $notificationAppName --resource-group $ResourceGroupName --xml 2>$null
    $frontendProfile = & $azCmd webapp deployment list-publishing-profiles --name $frontendAppName --resource-group $ResourceGroupName --xml 2>$null

    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host " GITHUB REPOSITORY APP NAMES & SECRETS " -ForegroundColor Green
    Write-Host " (https://github.com/$GitHubRepo/settings/secrets/actions)" -ForegroundColor Green
    Write-Host "========================================================`n" -ForegroundColor Green

    Write-Host "1. AZURE_IDENTITY_SERVICE_APP_NAME     = $identityAppName"
    Write-Host "2. AZURE_FLEET_SERVICE_APP_NAME        = $fleetAppName"
    Write-Host "3. AZURE_RENTAL_SERVICE_APP_NAME       = $rentalAppName"
    Write-Host "4. AZURE_NOTIFICATION_SERVICE_APP_NAME = $notificationAppName"
    Write-Host "5. AZURE_FRONTEND_APP_NAME             = $frontendAppName"
    
    Write-Host "`nPublish Profiles have been retrieved. Set these in GitHub Secrets as:"
    Write-Host "- AZURE_IDENTITY_SERVICE_PUBLISH_PROFILE"
    Write-Host "- AZURE_FLEET_SERVICE_PUBLISH_PROFILE"
    Write-Host "- AZURE_RENTAL_SERVICE_PUBLISH_PROFILE"
    Write-Host "- AZURE_NOTIFICATION_SERVICE_PUBLISH_PROFILE"
    Write-Host "- AZURE_FRONTEND_PUBLISH_PROFILE"
}
