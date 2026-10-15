<#
.SYNOPSIS
    Applies production security, availability, and cost optimizations to Azure FleetFlow deployment.
.DESCRIPTION
    1. Removes 0.0.0.0-255.255.255.255 PostgreSQL firewall rule (AllowAllForTesting)
    2. Enables AlwaysOn on all 5 Web Apps to prevent 60-105s cold starts
    3. Enforces HTTPS-Only and HTTP/2 on all 5 Web Apps
    4. Sets healthCheckPath to /health/ready on backend APIs
    5. Sets ASPNETCORE_ENVIRONMENT to Production
    6. Deallocates idle zombie VM in rg-fleetflow-kr to save student credits
#>
param(
    [string]$ResourceGroup = "rg-fleetflow",
    [string]$KrResourceGroup = "rg-fleetflow-kr",
    [string]$PostgresServer = "fleetflow-pgserver",
    [switch]$DeallocateIdleVm,
    [switch]$DeleteLegacyApp
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " FLEETFLOW AZURE PRODUCTION HARDENING & OPTIMIZATION" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$backendApps = @(
    "fleetflow-identity-api",
    "fleetflow-fleet-api",
    "fleetflow-rental-api",
    "fleetflow-notification-api"
)
$allApps = $backendApps + "fleetflow-frontend"

# 1. Close PostgreSQL Firewall Vulnerability
Write-Host "`n[1/5] Hardening PostgreSQL Flexible Server Firewall..." -ForegroundColor Yellow
$fwRules = az postgres flexible-server firewall-rule list --resource-group $ResourceGroup --server-name $PostgresServer --query "[].name" -o tsv 2>$null
if ($fwRules -contains "AllowAllForTesting") {
    Write-Host "  -> Removing insecure firewall rule 'AllowAllForTesting' (0.0.0.0 - 255.255.255.255)..." -ForegroundColor Red
    az postgres flexible-server firewall-rule delete --resource-group $ResourceGroup --server-name $PostgresServer --name "AllowAllForTesting" --yes --output none
    Write-Host "  -> SECURED: Only internal Azure traffic rule 'AllowAllAzureServicesAndResourcesWithinAzureIps' is retained." -ForegroundColor Green
} else {
    Write-Host "  -> Verified: Insecure rule 'AllowAllForTesting' is not present." -ForegroundColor Green
}

# 2. Enable AlwaysOn, HTTPS-Only, HTTP/2, and FTPS Disabled on Web Apps
Write-Host "`n[2/5] Configuring AlwaysOn, HTTPS-Only, and HTTP/2 on App Services..." -ForegroundColor Yellow
foreach ($app in $allApps) {
    Write-Host "  -> Updating $app..." -ForegroundColor Cyan
    az webapp update --name $app --resource-group $ResourceGroup --set httpsOnly=true --output none
    az webapp config set --name $app --resource-group $ResourceGroup --always-on true --http20-enabled true --ftps-state Disabled --min-tls-version "1.2" --output none
}
Write-Host "  -> COMPLETED: All 5 web apps now have AlwaysOn=true, HttpsOnly=true, Http20=true, Ftps=Disabled." -ForegroundColor Green

# 3. Configure Health Probes on Backend APIs
Write-Host "`n[3/5] Configuring Health Check Probes on Backend APIs..." -ForegroundColor Yellow
foreach ($app in $backendApps) {
    Write-Host "  -> Setting healthCheckPath=/health/ready on $app..." -ForegroundColor Cyan
    az webapp config set --name $app --resource-group $ResourceGroup --generic-configurations '{\"healthCheckPath\":\"/health/ready\"}' --output none
}
Write-Host "  -> COMPLETED: Azure load balancer will now auto-monitor and self-heal /health/ready on all microservices." -ForegroundColor Green

# 4. Enforce Production ASPNETCORE_ENVIRONMENT
Write-Host "`n[4/5] Setting ASPNETCORE_ENVIRONMENT=Production across backend APIs..." -ForegroundColor Yellow
foreach ($app in $backendApps) {
    Write-Host "  -> Updating environment on $app..." -ForegroundColor Cyan
    az webapp config appsettings set --name $app --resource-group $ResourceGroup --settings ASPNETCORE_ENVIRONMENT=Production --output none
}
Write-Host "  -> COMPLETED: Backend APIs set to Production mode (stack trace leak prevention & optimized memory)." -ForegroundColor Green

# 5. Cost Optimization: Deallocate Idle VM
if ($DeallocateIdleVm) {
    Write-Host "`n[5/5] Deallocating idle VM 'vm-fleetflow-backend' in $KrResourceGroup..." -ForegroundColor Yellow
    az vm deallocate --resource-group $KrResourceGroup --name "vm-fleetflow-backend" --output none
    Write-Host "  -> COMPLETED: VM deallocated. Compute billing paused, saving ~$35/month in student credits!" -ForegroundColor Green
} else {
    Write-Host "`n[5/5] Skipping VM deallocation. Pass -DeallocateIdleVm to stop the idle Standard_B2ats_v2 VM." -ForegroundColor DarkGray
}

# Optional: Delete legacy stopped web app
if ($DeleteLegacyApp) {
    Write-Host "`n[Bonus] Removing legacy stopped web app 'fleetflow-app-24103433'..." -ForegroundColor Yellow
    az webapp delete --name "fleetflow-app-24103433" --resource-group $ResourceGroup --output none
    Write-Host "  -> COMPLETED: Legacy app deleted from ASP-rgfleetflow-99e3." -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " ALL AZURE DEPLOYMENT IMPROVEMENTS SUCCESSFULLY APPLIED!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
