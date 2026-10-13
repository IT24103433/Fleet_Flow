[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('rental','notifications')][string]$Service,[switch]$Apply)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'LocalEnvironment.ps1')
Import-FleetFlowLocalEnvironment $repositoryRoot
$project = Join-Path $PSScriptRoot 'ServiceDatabaseTool/ServiceDatabaseTool.csproj'
$arguments = @('run','--project',$project,'--','--service',$Service)
if ($Apply) { $arguments += '--apply' }
& dotnet @arguments
exit $LASTEXITCODE
