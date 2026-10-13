[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('identity','fleet','rental','notifications')][string]$Service)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'LocalEnvironment.ps1')
Import-FleetFlowLocalEnvironment $repositoryRoot
$descriptors = @{
    identity = @{ Name='IdentityService'; Port=5001; DatabaseKey='AUTH_DB_NAME'; Database='fleetflow_auth' }
    fleet = @{ Name='FleetService'; Port=5002; DatabaseKey='FLEET_DB_NAME'; Database='fleetflow_fleet' }
    rental = @{ Name='RentalService'; Port=5003; DatabaseKey='RENTAL_DB_NAME'; Database='fleetflow_rental' }
    notifications = @{ Name='NotificationService'; Port=5004; DatabaseKey='NOTIFICATION_DB_NAME'; Database='fleetflow_notifications' }
}
$descriptor = $descriptors[$Service]
$configuredDatabase = [Environment]::GetEnvironmentVariable($descriptor.DatabaseKey,'Process')
if ($configuredDatabase -and $configuredDatabase -ne $descriptor.Database) { throw 'Unexpected local database name; refusing startup.' }
[Environment]::SetEnvironmentVariable($descriptor.DatabaseKey,$descriptor.Database,'Process')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://127.0.0.1:$($descriptor.Port)"
if ($env:JWT_SECRET) { $env:Jwt__Key = $env:JWT_SECRET }
if ($env:JWT_ISSUER) { $env:Jwt__Issuer = $env:JWT_ISSUER }
if ($env:JWT_AUDIENCE) { $env:Jwt__Audience = $env:JWT_AUDIENCE }
if ($env:KAFKA_ENABLED) { $env:Kafka__Enabled = $env:KAFKA_ENABLED }
if ($env:KAFKA_BOOTSTRAP_SERVERS) { $env:Kafka__BootstrapServers = $env:KAFKA_BOOTSTRAP_SERVERS }
$project = Join-Path $repositoryRoot "src/backend/services/$($descriptor.Name)/$($descriptor.Name).Api/$($descriptor.Name).Api.csproj"
Write-Host "Starting $($descriptor.Name) at $env:ASPNETCORE_URLS. Existing processes are not stopped or replaced."
& dotnet run --project $project --no-launch-profile
exit $LASTEXITCODE
