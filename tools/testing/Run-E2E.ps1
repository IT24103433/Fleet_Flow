[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ResultsDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=if([IO.Path]::IsPathRooted($ResultsDirectory)){$ResultsDirectory}else{Join-Path $repo $ResultsDirectory}
$output=[IO.Path]::GetFullPath($output)
if(Test-Path -LiteralPath $output){throw 'Choose a fresh results directory; evidence is never overwritten.'}
if(-not $env:ASE_TEST_PASSWORD){throw 'ASE_TEST_PASSWORD is required'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$previous=$env:RESULTS_DIR
$metadata=[ordered]@{owner='Varun K.';studentId='IT24103696';startedUtc=[DateTime]::UtcNow.ToString('O');sourceCommit=(& git -C $repo rev-parse HEAD).Trim();sourceHadUncommittedChanges=[bool](& git -C $repo status --porcelain);browserChannel=$env:ASE_BROWSER_CHANNEL;exitCode=$null;completedUtc=$null}
$status=1
Push-Location (Join-Path $repo 'tests/browser')
try{
 $env:RESULTS_DIR=$output
 & node node_modules/@playwright/test/cli.js test 2>&1 | Tee-Object -FilePath (Join-Path $output 'execution.log')
 $status=$LASTEXITCODE
 if(-not(Test-Path (Join-Path $output 'results.json'))){throw 'Playwright did not generate results.json'}
 $summary=Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw|ConvertFrom-Json
 if($summary.stats.expected -le 0 -or $summary.stats.unexpected -ne 0 -or $summary.stats.skipped -ne 0 -or $summary.stats.flaky -ne 0){throw 'Browser results were empty, failing, skipped or flaky'}
}catch{Write-Warning $_.Exception.Message;$status=1}finally{
 Pop-Location
 $env:RESULTS_DIR=$previous
 $metadata.exitCode=$status;$metadata.completedUtc=[DateTime]::UtcNow.ToString('O')
 $metadata|ConvertTo-Json -Depth 5|Set-Content (Join-Path $output 'run.json') -Encoding utf8
}
exit $status
