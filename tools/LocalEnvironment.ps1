# Dot-source only from the local startup/initialization helpers.
function Import-FleetFlowLocalEnvironment([string]$RepositoryRoot) {
    $environmentFile = Join-Path $RepositoryRoot '.env'
    if (Test-Path -LiteralPath $environmentFile) {
        foreach ($line in Get-Content -LiteralPath $environmentFile) {
            if ($line -match '^\s*(#|$)') { continue }
            if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
                throw 'Unsupported .env syntax. Use simple KEY=value lines; no shell commands.'
            }
            $name = $Matches[1]
            $value = $Matches[2].Trim()
            if ($name -in @('HOME','CODEX_HOME','PATH','PSModulePath')) { throw "Reserved environment name: $name" }
            if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1,$value.Length-2) }
            [Environment]::SetEnvironmentVariable($name,$value,'Process')
        }
    }
    if ($env:DB_HOST -and $env:DB_HOST -notin @('localhost','127.0.0.1','::1')) {
        throw 'This helper only starts services against loopback PostgreSQL. Deployment configuration is separate.'
    }
    if ($env:ConnectionStrings__DefaultConnection) {
        throw 'Remove the inherited ConnectionStrings__DefaultConnection override before local startup; use DB_* settings.'
    }
}
