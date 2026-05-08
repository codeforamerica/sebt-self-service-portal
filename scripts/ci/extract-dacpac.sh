#!/bin/bash
# DACPAC Extraction Script
# Produces a deliverable DACPAC from the current EF Core schema.
#
# Applies all migrations against a clean throwaway database, then extracts a
# DACPAC that includes the __EFMigrationsHistory table data so that the app's
# startup migration check sees the schema as fully up to date.
#
# Cross-platform: macOS, Linux, Windows (Git Bash / WSL).
#
# Usage:
#   ./scripts/ci/extract-dacpac.sh [--output <path>] [--password <pwd>]
#                                  [--port <port>] [--host <host>]
#                                  [--keep-db] [--skip-container-start]
#
# Prerequisites:
#   - docker (with compose plugin)
#   - dotnet SDK + dotnet-ef tool (dotnet tool install --global dotnet-ef)
#   - sqlpackage (dotnet tool install --global Microsoft.SqlPackage)

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Defaults (mirror compose.yaml)
MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-YourStrong@Passw0rd}"
MSSQL_PORT="${MSSQL_PORT:-1433}"
MSSQL_HOST="${MSSQL_HOST:-localhost}"
MSSQL_USER="${MSSQL_USER:-sa}"
DATE_STAMP=$(date +%Y%m%d)
OUTPUT_DACPAC="$PROJECT_ROOT/output/sebt-portal-${DATE_STAMP}.dacpac"
TEMP_DB="SebtDacpacExtract_${DATE_STAMP}_$$"
KEEP_DB=false
SKIP_CONTAINER_START=false

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case $1 in
    --output)
      OUTPUT_DACPAC="$2"
      shift 2
      ;;
    --password)
      MSSQL_SA_PASSWORD="$2"
      shift 2
      ;;
    --port)
      MSSQL_PORT="$2"
      shift 2
      ;;
    --host)
      MSSQL_HOST="$2"
      shift 2
      ;;
    --keep-db)
      KEEP_DB=true
      shift
      ;;
    --skip-container-start)
      SKIP_CONTAINER_START=true
      shift
      ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      log_error "Unknown argument: $1"
      exit 1
      ;;
  esac
done

check_prereqs() {
  log_info "Checking prerequisites..."

  command -v dotnet >/dev/null 2>&1 || {
    log_error "dotnet SDK not found on PATH"
    exit 1
  }

  command -v docker >/dev/null 2>&1 || {
    log_error "docker not found on PATH"
    exit 1
  }

  command -v sqlpackage >/dev/null 2>&1 || {
    log_error "sqlpackage not found on PATH"
    log_info "Install with:  dotnet tool install --global Microsoft.SqlPackage"
    log_info "Make sure ~/.dotnet/tools is on your PATH."
    exit 1
  }

  if ! dotnet ef --version >/dev/null 2>&1; then
    log_error "dotnet-ef tool is not installed"
    log_info "Install with:  dotnet tool install --global dotnet-ef"
    exit 1
  fi

  log_success "All prerequisites present"
}

exec_sql() {
  # Runs a T-SQL statement inside the mssql container against the master DB.
  docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U "$MSSQL_USER" -P "$MSSQL_SA_PASSWORD" -C -b \
    -Q "$1"
}

drop_temp_db() {
  local db="$1"
  exec_sql "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" \
    >/dev/null 2>&1 || true
}

start_mssql() {
  if [ "$SKIP_CONTAINER_START" = true ]; then
    log_info "Skipping container start (--skip-container-start)"
    return 0
  fi

  log_info "Starting MSSQL container..."
  (cd "$PROJECT_ROOT" && docker compose up -d mssql) >/dev/null

  log_info "Waiting for MSSQL to become ready..."
  local retries=30
  while [ $retries -gt 0 ]; do
    if exec_sql "SELECT 1" >/dev/null 2>&1; then
      log_success "MSSQL is ready"
      return 0
    fi
    retries=$((retries - 1))
    sleep 2
  done

  log_error "MSSQL did not become ready in time"
  exit 1
}

cleanup() {
  local exit_code=$?
  if [ "$KEEP_DB" = true ]; then
    log_warning "Leaving temp DB in place: $TEMP_DB"
  else
    log_info "Dropping temp DB: $TEMP_DB"
    drop_temp_db "$TEMP_DB"
  fi
  exit $exit_code
}
trap cleanup EXIT

apply_migrations() {
  log_info "Preparing temp DB: $TEMP_DB"
  drop_temp_db "$TEMP_DB"

  local conn="Server=${MSSQL_HOST},${MSSQL_PORT};Database=${TEMP_DB};User Id=${MSSQL_USER};Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;"

  log_info "Applying EF migrations to temp DB..."
  cd "$PROJECT_ROOT"
  dotnet ef database update \
    --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
    --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj \
    --connection "$conn"

  log_success "Migrations applied"
}

extract_dacpac() {
  log_info "Extracting DACPAC (including __EFMigrationsHistory data)..."
  mkdir -p "$(dirname "$OUTPUT_DACPAC")"
  rm -f "$OUTPUT_DACPAC"

  local conn="Server=${MSSQL_HOST},${MSSQL_PORT};Database=${TEMP_DB};User Id=${MSSQL_USER};Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;"

  # TableData is set to only __EFMigrationsHistory so the DACPAC includes the
  # migrations table contents (the app's startup check sees the schema as fully
  # up to date) but no other table data.
  sqlpackage \
    /Action:Extract \
    /SourceConnectionString:"$conn" \
    /TargetFile:"$OUTPUT_DACPAC" \
    /p:ExtractAllTableData=false \
    /p:TableData="[dbo].[__EFMigrationsHistory]" \
    /p:ExtractApplicationScopedObjectsOnly=true \
    /p:IgnorePermissions=true \
    /p:IgnoreUserLoginMappings=true \
    /p:VerifyExtraction=true

  log_success "DACPAC written: $OUTPUT_DACPAC"
  log_info "Size: $(du -h "$OUTPUT_DACPAC" | cut -f1)"
}

main() {
  log_info "=== SEBT Portal DACPAC Extract ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Output:       $OUTPUT_DACPAC"
  log_info "Temp DB:      $TEMP_DB"
  log_info "MSSQL:        ${MSSQL_HOST}:${MSSQL_PORT}"
  echo ""

  check_prereqs
  start_mssql
  apply_migrations
  extract_dacpac

  echo ""
  log_success "=== DACPAC extract complete ==="
  log_info "Deliverable: $OUTPUT_DACPAC"
  echo ""
  log_info "For the receiving admin to preview the delta before applying:"
  log_info "  sqlpackage /Action:DeployReport \\"
  log_info "    /SourceFile:$(basename "$OUTPUT_DACPAC") \\"
  log_info "    /TargetConnectionString:'<their conn>' \\"
  log_info "    /OutputPath:deploy-report.xml"
  echo ""
  log_info "To apply:"
  log_info "  sqlpackage /Action:Publish \\"
  log_info "    /SourceFile:$(basename "$OUTPUT_DACPAC") \\"
  log_info "    /TargetConnectionString:'<their conn>'"
}

main
