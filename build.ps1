<#
.SYNOPSIS
    Unified build, test, and verification script for FleetFlow.

.DESCRIPTION
    Automates dependency restoration, compilation, test execution, and packaging
    for both the .NET backend microservices and the Vite/React frontend.

.PARAMETER Configuration
    Build configuration: 'Release' (default) or 'Debug'.

.PARAMETER SkipTests
    If set, skips running backend and frontend automated tests.

.PARAMETER SkipBackend
    If set, skips building and testing .NET backend services.

.PARAMETER SkipFrontend
    If set, skips building and testing the React frontend.

.PARAMETER Clean
    If set, cleans build artifacts (bin, obj, dist) before compiling.

.EXAMPLE
    .\build.ps1
    Builds and tests the entire solution in Release configuration.

.EXAMPLE
    .\build.ps1 -Configuration Debug -SkipTests
    Quickly builds backend and frontend without running tests.
#>

[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$SkipTests,
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

function Write-StepHeader([string]$title) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host "  >> $title" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
}

function Write-Success([string]$msg) {
    Write-Host "[SUCCESS] $msg" -ForegroundColor Green
}

function Write-Failure([string]$msg) {
    Write-Host "[ERROR] $msg" -ForegroundColor Red
}

function Write-Info([string]$msg) {
    Write-Host "[INFO] $msg" -ForegroundColor Yellow
}

$RootDir = $PSScriptRoot
$BackendSln = Join-Path $RootDir "FleetFlow.sln"
$FrontendDir = Join-Path $RootDir "src\frontend"

Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host "           FLEETFLOW UNIFIED BUILD & VERIFICATION SYSTEM              " -ForegroundColor Magenta
Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host " Root Directory : $RootDir"
Write-Host " Configuration  : $Configuration"
Write-Host " Run Tests      : $(-not $SkipTests)"
Write-Host " Target Backend : $(-not $SkipBackend)"
Write-Host " Target Frontend: $(-not $SkipFrontend)"
Write-Host " Clean Build    : $Clean"
Write-Host " Timestamp      : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

# 1. Prerequisite Validation
Write-StepHeader "1. Validating Environment Prerequisites"

# Check .NET SDK
try {
    $dotnetVersion = & dotnet --version
    Write-Success ".NET SDK detected: v$dotnetVersion"
}
catch {
    Write-Failure ".NET SDK not found in PATH. Please install .NET SDK 10.0 or later."
    exit 1
}

# Check Node.js
try {
    $nodeVersion = & node --version
    Write-Success "Node.js detected: $nodeVersion"
}
catch {
    Write-Failure "Node.js not found in PATH. Please install Node.js v22.x or later."
    exit 1
}

# Check npm
try {
    $npmVersion = & npm --version
    Write-Success "npm detected: v$npmVersion"
}
catch {
    Write-Failure "npm not found in PATH."
    exit 1
}

# 2. Cleaning Artifacts (if requested)
if ($Clean) {
    Write-StepHeader "2. Cleaning Prior Build Artifacts"
    if (-not $SkipBackend) {
        Write-Info "Cleaning .NET solution artifacts..."
        & dotnet clean "$BackendSln" --configuration $Configuration -v quiet
        Get-ChildItem -Path $RootDir -Include bin,obj -Recurse -Directory -ErrorAction SilentlyContinue | 
            Where-Object { $_.FullName -notmatch "node_modules" } |
            ForEach-Object {
                Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        Write-Success "Backend artifacts cleaned."
    }
    if (-not $SkipFrontend) {
        Write-Info "Cleaning Frontend dist..."
        $distPath = Join-Path $FrontendDir "dist"
        if (Test-Path $distPath) {
            Remove-Item -Path $distPath -Recurse -Force
        }
        Write-Success "Frontend build artifacts cleaned."
    }
}

# 3. Backend Pipeline
if (-not $SkipBackend) {
    Write-StepHeader "3. Building Backend Solution (FleetFlow.sln)"
    Write-Info "Restoring NuGet dependencies..."
    & dotnet restore "$BackendSln"
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Backend dependency restore failed."
        exit $LASTEXITCODE
    }
    Write-Success "NuGet dependencies restored."

    Write-Info "Compiling backend services and test projects in $Configuration mode..."
    & dotnet build "$BackendSln" --no-restore --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Backend compilation failed."
        exit $LASTEXITCODE
    }
    Write-Success "Backend compiled successfully."

    if (-not $SkipTests) {
        Write-StepHeader "4. Executing Backend Automated Tests"
        $resultsDir = Join-Path $RootDir "TestResults"
        if (-not (Test-Path $resultsDir)) {
            New-Item -ItemType Directory -Path $resultsDir | Out-Null
        }
        $trxLog = "trx;LogFileName=backend-results.trx"
        & dotnet test "$BackendSln" --no-build --configuration $Configuration --logger $trxLog --results-directory "$resultsDir"
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Backend tests failed."
            exit $LASTEXITCODE
        }
        Write-Success "All backend unit and integration test suites passed."
    }
}

# 4. Frontend Pipeline
if (-not $SkipFrontend) {
    Write-StepHeader "5. Building Frontend Application"
    Push-Location $FrontendDir
    try {
        $npmExec = "npm.cmd"
        if (-not (Get-Command "npm.cmd" -ErrorAction SilentlyContinue)) {
            $npmExec = "npm"
        }

        Write-Info "Installing / verifying frontend npm packages..."
        if (Test-Path "package-lock.json") {
            & $npmExec ci
        } else {
            & $npmExec install
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Frontend npm package installation failed."
            exit $LASTEXITCODE
        }
        Write-Success "Frontend dependencies installed."

        Write-Info "Running frontend ESLint code quality check..."
        & $npmExec run lint
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Frontend linting check failed."
            exit $LASTEXITCODE
        }
        Write-Success "Frontend code quality check passed."

        if (-not $SkipTests) {
            Write-StepHeader "6. Executing Frontend Automated Tests"
            Write-Info "Running client-side test suite..."
            & $npmExec test -- --run
            if ($LASTEXITCODE -ne 0) {
                Write-Failure "Frontend tests failed."
                exit $LASTEXITCODE
            }
            Write-Success "All frontend client tests passed."
        }

        Write-StepHeader "7. Generating Frontend Production Bundle"
        Write-Info "Building Vite client assets..."
        & $npmExec run build
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Frontend Vite production build failed."
            exit $LASTEXITCODE
        }
        Write-Success "Frontend production bundle successfully compiled to dist/."
    }
    finally {
        Pop-Location
    }
}

# Summary
$Duration = (Get-Date) - $StartTime
Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Green
Write-Host "                FLEETFLOW BUILD & TEST SUITE PASSED               " -ForegroundColor Green
Write-Host ("=" * 70) -ForegroundColor Green
Write-Host " Total Duration : $($Duration.ToString('mm\:ss'))"
Write-Host " Status         : 100% GREEN - READY FOR DEPLOYMENT / PRESENTATION"
Write-Host ""
exit 0
