[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ResultsDirectory,
    [string]$PythonExecutable='python3',
    [switch]$Baseline
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=if([IO.Path]::IsPathRooted($ResultsDirectory)){[IO.Path]::GetFullPath($ResultsDirectory)}else{[IO.Path]::GetFullPath((Join-Path $repo $ResultsDirectory))}
if(Test-Path -LiteralPath $output){throw 'Results directory exists. Choose a fresh path to preserve previous evidence.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$project=Join-Path $repo 'tests/FleetFlow.VehicleService.Tests'
$config=Join-Path $project 'stryker-config.json'
$filter=if($Baseline){'Category=ASE.Unit'}else{'Category=ASE.Unit|Category=ASE.Mutation'}
$status=1
$metadata=[ordered]@{
    owner='Jadavan M. (IT24103433)'
    area='Mutation Testing'
    sourceCommit=(& git -C $repo rev-parse HEAD).Trim()
    sourceHadUncommittedChanges=[bool](& git -C $repo status --porcelain --untracked-files=normal)
    startedUtc=[DateTime]::UtcNow.ToString('O')
    localTimeZone=[TimeZoneInfo]::Local.Id
    completedUtc=$null
    baseline=[bool]$Baseline
    testFilter=$filter
    scope=@('Services/ReportCsv.cs','Services/MaintenanceStaffDirectory.cs')
    exitCode=$null
    error=$null
}
Copy-Item -LiteralPath $config -Destination (Join-Path $output 'stryker-config.json')
$effective=Get-Content -LiteralPath $config -Raw|ConvertFrom-Json
$effective.'stryker-config'.'test-case-filter'=$filter
$effective|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $output 'effective-config.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repo 'dotnet-tools.json') -Destination (Join-Path $output 'dotnet-tools.json')
$sources=@(& git -C $repo ls-files --cached --others --exclude-standard '*.cs' '*.csproj')
$hashes=@(foreach($relative in $sources){
    $file=Join-Path $repo $relative
    if(Test-Path -LiteralPath $file){[ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}}
})
$hashes|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'source-files.json') -Encoding utf8
Push-Location $repo
try{
    & dotnet --info 2>&1 | Out-File -LiteralPath (Join-Path $output 'dotnet-info.txt') -Encoding utf8
    if($LASTEXITCODE -ne 0){throw '.NET runtime check failed'}
    & dotnet tool restore 2>&1 | Tee-Object -FilePath (Join-Path $output 'tool-restore.log')
    if($LASTEXITCODE -ne 0){throw 'Local Stryker tool restore failed'}
    & dotnet test (Join-Path $project 'FleetFlow.VehicleService.Tests.csproj') --filter $filter --logger 'trx;LogFileName=preflight.trx' --results-directory (Join-Path $output 'preflight') 2>&1 | Tee-Object -FilePath (Join-Path $output 'preflight.log')
    if($LASTEXITCODE -ne 0){throw 'Unmutated test baseline failed; mutation execution was not started'}
    [xml]$preflight=Get-Content -LiteralPath (Join-Path $output 'preflight/preflight.trx') -Raw
    $counters=$preflight.TestRun.ResultSummary.Counters
    if([int]$counters.total -le 0 -or [int]$counters.passed -ne [int]$counters.total){throw 'Preflight has empty, failed or skipped cases; mutation execution was not started'}
    Push-Location $project
    try{
        & dotnet stryker --config-file (Join-Path $output 'effective-config.json') --output (Join-Path $output 'stryker') --skip-version-check --break-on-initial-test-failure 2>&1 | Tee-Object -FilePath (Join-Path $output 'execution.log')
        $status=$LASTEXITCODE
    }finally{Pop-Location}
}catch{
    $metadata.error=$_.Exception.Message
    Write-Warning $metadata.error
    $status=1
}finally{
    Pop-Location
    $metadata.exitCode=$status
    $metadata.completedUtc=[DateTime]::UtcNow.ToString('O')
    $metadata|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'run.json') -Encoding utf8
}
& $PythonExecutable (Join-Path $PSScriptRoot 'summarise_mutation.py') --results $output
$summaryExit=$LASTEXITCODE
if($status -ne 0){exit $status}
exit $summaryExit
