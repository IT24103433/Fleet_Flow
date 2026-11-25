[CmdletBinding()]
param(
 [ValidateSet('smoke','baseline','ramp','stress')][string]$Profile='smoke',
 [Parameter(Mandatory=$true)][string]$ResultsDirectory,
 [string]$K6Executable='k6',
 [switch]$AllowStress
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=if([IO.Path]::IsPathRooted($ResultsDirectory)){$ResultsDirectory}else{Join-Path $repo $ResultsDirectory}
$output=[IO.Path]::GetFullPath($output)
if(Test-Path -LiteralPath $output){throw 'Choose a fresh results directory; evidence is never overwritten.'}
if(-not $env:ASE_TEST_PASSWORD){throw 'ASE_TEST_PASSWORD is required'}
if($Profile -eq 'stress' -and -not $AllowStress){throw 'Use -AllowStress to enable the bounded local stress run'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$previous=@{PROFILE=$env:PROFILE;RESULTS_DIR=$env:RESULTS_DIR;ALLOW_STRESS=$env:ALLOW_STRESS}
$metadata=[ordered]@{owner='Aqqil M.R.M.';studentId='IT24104314';profile=$Profile;startedUtc=[DateTime]::UtcNow.ToString('O');sourceCommit=(& git -C $repo rev-parse HEAD).Trim();sourceHadUncommittedChanges=[bool](& git -C $repo status --porcelain);exitCode=$null;completedUtc=$null}
$status=1
try{
 $env:PROFILE=$Profile;$env:RESULTS_DIR=$output;$env:ALLOW_STRESS=if($AllowStress){'1'}else{'0'}
 & $K6Executable version 2>&1 | Out-File (Join-Path $output 'k6-version.txt') -Encoding utf8
 if($LASTEXITCODE -ne 0){throw 'k6 version check failed'}
 & $K6Executable run --quiet (Join-Path $repo 'tests/load/fleetflow.js') 2>&1 | Tee-Object -FilePath (Join-Path $output 'execution.log')
 $status=$LASTEXITCODE
 if(-not(Test-Path (Join-Path $output 'summary.json'))){throw 'k6 did not generate summary.json'}
 $summary=Get-Content -LiteralPath (Join-Path $output 'summary.json') -Raw|ConvertFrom-Json
 if($summary.metrics.http_reqs.values.count -le 0){throw 'No actual HTTP requests were recorded'}
}catch{Write-Warning $_.Exception.Message;$status=1}finally{
 foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
 $metadata.exitCode=$status;$metadata.completedUtc=[DateTime]::UtcNow.ToString('O')
 $metadata|ConvertTo-Json -Depth 5|Set-Content (Join-Path $output 'run.json') -Encoding utf8
}
exit $status
