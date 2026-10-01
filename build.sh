#!/usr/bin/env bash
# ==============================================================================
# FleetFlow Unified Build & Verification Script (Linux / macOS / CI)
# ==============================================================================
set -e

CONFIGURATION="Release"
SKIP_TESTS=false
SKIP_BACKEND=false
SKIP_FRONTEND=false
CLEAN=false

for arg in "$@"; do
  case $arg in
    --configuration=*)
      CONFIGURATION="${arg#*=}"
      shift
      ;;
    --skip-tests)
      SKIP_TESTS=true
      shift
      ;;
    --skip-backend)
      SKIP_BACKEND=true
      shift
      ;;
    --skip-frontend)
      SKIP_FRONTEND=true
      shift
      ;;
    --clean)
      CLEAN=true
      shift
      ;;
    *)
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_SLN="$SCRIPT_DIR/FleetFlow.sln"
FRONTEND_DIR="$SCRIPT_DIR/src/frontend"

echo "======================================================================"
echo "           FLEETFLOW UNIFIED BUILD & VERIFICATION SYSTEM              "
echo "======================================================================"
echo " Root Directory : $SCRIPT_DIR"
echo " Configuration  : $CONFIGURATION"
echo " Run Tests      : $([ "$SKIP_TESTS" = false ] && echo 'true' || echo 'false')"
echo " Target Backend : $([ "$SKIP_BACKEND" = false ] && echo 'true' || echo 'false')"
echo " Target Frontend: $([ "$SKIP_FRONTEND" = false ] && echo 'true' || echo 'false')"
echo " Clean Build    : $CLEAN"
echo "======================================================================"

# 1. Prerequisite Checks
echo -e "\n>> 1. Validating Environment Prerequisites"
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET SDK is not installed or not in PATH."
    exit 1
fi
echo "[SUCCESS] .NET SDK detected: $(dotnet --version)"

if ! command -v node &> /dev/null; then
    echo "[ERROR] Node.js is not installed or not in PATH."
    exit 1
fi
echo "[SUCCESS] Node.js detected: $(node --version)"

if ! command -v npm &> /dev/null; then
    echo "[ERROR] npm is not installed or not in PATH."
    exit 1
fi
echo "[SUCCESS] npm detected: $(npm --version)"

# 2. Clean
if [ "$CLEAN" = true ]; then
  echo -e "\n>> 2. Cleaning Prior Build Artifacts"
  if [ "$SKIP_BACKEND" = false ]; then
    echo "[INFO] Cleaning .NET solution..."
    dotnet clean "$BACKEND_SLN" --configuration "$CONFIGURATION" -v quiet
    find "$SCRIPT_DIR" -type d \( -name "bin" -o -name "obj" \) -not -path "*/node_modules/*" -exec rm -rf {} + 2>/dev/null || true
  fi
  if [ "$SKIP_FRONTEND" = false ]; then
    echo "[INFO] Cleaning Frontend dist..."
    rm -rf "$FRONTEND_DIR/dist"
  fi
fi

# 3. Backend Pipeline
if [ "$SKIP_BACKEND" = false ]; then
  echo -e "\n>> 3. Building Backend Solution"
  echo "[INFO] Restoring NuGet dependencies..."
  dotnet restore "$BACKEND_SLN"
  
  echo "[INFO] Compiling backend services and tests ($CONFIGURATION)..."
  dotnet build "$BACKEND_SLN" --no-restore --configuration "$CONFIGURATION"
  
  if [ "$SKIP_TESTS" = false ]; then
    echo -e "\n>> 4. Executing Backend Automated Tests"
    mkdir -p "$SCRIPT_DIR/TestResults"
    dotnet test "$BACKEND_SLN" --no-build --configuration "$CONFIGURATION" --logger "trx;LogFileName=backend-results.trx" --results-directory "$SCRIPT_DIR/TestResults"
    echo "[SUCCESS] All backend test suites passed."
  fi
fi

# 4. Frontend Pipeline
if [ "$SKIP_FRONTEND" = false ]; then
  echo -e "\n>> 5. Building Frontend Application"
  cd "$FRONTEND_DIR"
  
  echo "[INFO] Installing npm dependencies..."
  if [ -f "package-lock.json" ]; then
    npm ci
  else
    npm install
  fi
  
  echo "[INFO] Running ESLint check..."
  npm run lint
  
  if [ "$SKIP_TESTS" = false ]; then
    echo -e "\n>> 6. Executing Frontend Automated Tests"
    npm test -- --run
    echo "[SUCCESS] All frontend tests passed."
  fi
  
  echo -e "\n>> 7. Generating Frontend Production Bundle"
  npm run build
  echo "[SUCCESS] Frontend production build compiled to dist/."
  cd "$SCRIPT_DIR"
fi

echo -e "\n======================================================================"
echo "                FLEETFLOW BUILD & TEST SUITE PASSED               "
echo "======================================================================"
echo " Status: 100% GREEN - READY FOR DEPLOYMENT / PRESENTATION"
