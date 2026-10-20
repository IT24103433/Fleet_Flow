[CmdletBinding()]
param(
    [ValidateSet('Native','Docker')][string]$Engine='Native',
    [string]$ResultsDirectory='artifacts/ase-unit',
    [string]$PythonExecutable='python3',
    [string]$NuGetCache='',
    [switch]$IncludeExisting
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=if([IO.Path]::IsPathRooted($ResultsDirectory)){[IO.Path]::GetFullPath($ResultsDirectory)}else{[IO.Path]::GetFullPath((Join-Path $repo $ResultsDirectory))}
if(Test-Path -LiteralPath $output){throw 'Results directory exists. Select a new directory so earlier results are not overwritten.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$projects=@('FleetFlow.IdentityService.Tests','FleetFlow.VehicleService.Tests')
$testFilter=if($IncludeExisting){'FullyQualifiedName!~FleetFlow.IdentityService.Tests.OnboardingUniquenessTests'}else{'Category=ASE.Unit'}
$revision=(& git -C $repo rev-parse HEAD).Trim()
$dirty=[bool](& git -C $repo status --porcelain --untracked-files=normal)
$metadata=[ordered]@{
 owner='Sanjeevsaran J. (IT24104155)'
 area='Unit Testing and Mocking'
 sourceCommit=$revision
 sourceHadUncommittedChanges=$dirty
 runUtc=[DateTime]::UtcNow.ToString('O')
 engine=$Engine
 filter=$testFilter
 exclusions=if($IncludeExisting){'One existing PostgreSQL integration case is outside this offline unit run; retained in the ordinary backend CI job.'}else{'Non-ASE tests not selected'}
 network=if($Engine -eq 'Docker'){'disabled (--network none)'}else{'No running services required'}
 exitCode=$null
}
$status=0
Push-Location $repo
try{
 if($Engine -eq 'Docker'){
  $mounts=@('--mount',"type=bind,source=$repo,target=/source,readonly",'--mount',"type=bind,source=$output,target=/results")
  if($NuGetCache){$cache=[IO.Path]::GetFullPath($NuGetCache);if(-not(Test-Path -LiteralPath $cache)){throw 'NuGet cache does not exist'};$mounts+=@('--mount',"type=bind,source=$cache,target=/packages,readonly")}
  $filter='--filter '+$testFilter
  $script=@'
set -eu
dotnet --info
mkdir -p /work
tar -C /source --exclude='*/bin' --exclude='*/obj' -cf - src tests tools | tar -C /work -xf -
cd /work
status=0
for project in FleetFlow.IdentityService.Tests FleetFlow.VehicleService.Tests; do
  if dotnet test "tests/$project/$project.csproj" -c Release -p:NuGetAudit=false --collect:"XPlat Code Coverage" --settings tools/testing/unit.runsettings --logger trx --results-directory "/results/$project" --verbosity minimal FILTER_ARGUMENT; then :; else status=1; fi
done
exit "$status"
'@.Replace('FILTER_ARGUMENT',$filter)
  $script=$script.Replace("`r`n","`n").Replace("`r","`n")
  $envArgs=@('-e','DOTNET_CLI_TELEMETRY_OPTOUT=1')
  if($NuGetCache){$envArgs+=@('-e','NUGET_PACKAGES=/packages')}
  & docker run --rm --network none @mounts @envArgs mcr.microsoft.com/dotnet/sdk:10.0 sh -c $script 2>&1 | Tee-Object -FilePath (Join-Path $output 'execution.log')
  $status=$LASTEXITCODE
 }else{
  foreach($project in $projects){
   $arguments=@('test',"tests/$project/$project.csproj",'-c','Release','--collect:XPlat Code Coverage','--settings','tools/testing/unit.runsettings','--logger','trx','--results-directory',(Join-Path $output $project),'--verbosity','minimal')
   $arguments+=@('--filter',$testFilter)
   & dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output ($project+'.log'))
   if($LASTEXITCODE -ne 0){$status=1}
  }
 }
}catch{
 $status=1
 throw
}finally{
 Pop-Location
 $metadata.exitCode=$status
 $metadata|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'run.json') -Encoding utf8
}
& $PythonExecutable (Join-Path $PSScriptRoot 'summarise_unit.py') --results $output
$summaryExit=$LASTEXITCODE
if($status -ne 0){exit $status}
exit $summaryExit
