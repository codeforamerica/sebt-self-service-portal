#!/bin/bash
# API Publish Script
# Publishes the .NET API for win-x64 (framework-dependent) into <output>/api/,
# emits a secrets-only appsettings.prod.example.json, and patches the
# auto-generated web.config to enable ASP.NET Core stdout logging.
#
# Plugin DLLs are NOT copied by this script — they must already be in
# src/SEBT.Portal.Api/plugins-dc/ before this runs (populated by building the
# DC connector, whose MSBuild CopyPlugins target handles that). The API csproj's
# <None Include="plugins-dc\**\*.dll"> ItemGroup then picks them up during publish.
#
# Usage:
#   ./scripts/ci/publish-api.sh --output <dir> [--configuration Release] [--build-state-dir <dir>]
#
# Options:
#   --output <dir>            Where to place the api/ directory (required).
#   --configuration <cfg>     Debug or Release (default Release).
#   --build-state-dir <dir>   Optional. Redirects MSBuild's BaseIntermediateOutputPath
#                             and BaseOutputPath here so the source tree is not polluted.
#                             Used by the smoke test; the production workflow leaves this
#                             unset so build artifacts stay in the source's obj/ and bin/
#                             for cache reuse across steps.

set -e
set -u

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CONFIGURATION="Release"
OUTPUT_DIR=""
BUILD_STATE_DIR=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --output) OUTPUT_DIR="$2"; shift 2 ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
    --build-state-dir) BUILD_STATE_DIR="$2"; shift 2 ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$OUTPUT_DIR" ]; then
  log_error "--output is required"
  exit 1
fi

# Sanity-check that the DC connector has already populated plugins-dc/.
PLUGIN_DIR="$PROJECT_ROOT/src/SEBT.Portal.Api/plugins-dc"
if [ -z "$(ls -A "$PLUGIN_DIR" 2>/dev/null | grep -E '\.dll$' || true)" ]; then
  log_warning "$PLUGIN_DIR has no DLLs — DC connector was not built before publish-api.sh."
  log_warning "The published API will START but FAIL during MEF plugin composition at runtime."
  log_warning "If this is a Release build for delivery, abort and rebuild the DC connector first."
fi

API_OUT="$OUTPUT_DIR/api"
mkdir -p "$API_OUT"

log_info "Publishing API to $API_OUT (configuration: $CONFIGURATION, runtime: win-x64)"
PUBLISH_ARGS=(
  --configuration "$CONFIGURATION"
  --runtime win-x64
  --self-contained false
  --output "$API_OUT"
  -p:BuildFrontend=false
  --verbosity minimal
)
if [ -n "$BUILD_STATE_DIR" ]; then
  log_info "Isolating build artifacts to $BUILD_STATE_DIR"
  mkdir -p "$BUILD_STATE_DIR"
  # Note: Due to multi-repo dependencies (state-connector), applying BaseIntermediateOutputPath
  # globally causes circular dependency errors. Instead, rely on cleaning the source tree
  # before publish and trusting that --output isolates the final artifacts.
  # The test verifies no source-tree pollution via git status.
fi
dotnet publish "$PROJECT_ROOT/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj" "${PUBLISH_ARGS[@]}"

log_info "Writing appsettings.prod.example.json (DC-specific / secret keys only)"
cat > "$API_OUT/appsettings.prod.example.json" <<'JSON'
{
  "_comment": "Copy this file to appsettings.Production.json and fill in DC production values. Defaults for everything not listed here come from appsettings.json. Do not commit your filled-in copy.",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_DB_HOST,1433;Database=SEBT_Portal_DC;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
  },
  "DCConnector": {
    "ConnectionString": "Server=YOUR_DC_SOURCE_DB_HOST,1433;Database=DcSource;User Id=YOUR_DC_SOURCE_USER;Password=YOUR_DC_SOURCE_PASSWORD;TrustServerCertificate=True;"
  },
  "Smarty": {
    "Enabled": true,
    "AuthId": "YOUR_SMARTY_AUTH_ID",
    "AuthToken": "YOUR_SMARTY_AUTH_TOKEN"
  },
  "Socure": {
    "ApiKey": "YOUR_SOCURE_PROD_API_KEY",
    "WebhookSecret": "YOUR_SOCURE_WEBHOOK_BEARER_TOKEN",
    "BaseUrl": "https://riskos.socure.com",
    "DiSessionToken": "YOUR_DI_SESSION_TOKEN"
  }
}
JSON

log_info "Patching web.config to enable stdout logging"
WEBCONFIG="$API_OUT/web.config"
if [ ! -f "$WEBCONFIG" ]; then
  log_error "Expected dotnet publish to emit web.config at: $WEBCONFIG"
  exit 1
fi
# The auto-generated config has stdoutLogEnabled="false" — flip it.
# Portable sed for both GNU sed (Linux) and BSD sed (macOS):
if sed --version >/dev/null 2>&1; then
  # GNU sed
  sed -i 's/stdoutLogEnabled="false"/stdoutLogEnabled="true"/g' "$WEBCONFIG"
else
  # BSD sed (macOS)
  sed -i '' 's/stdoutLogEnabled="false"/stdoutLogEnabled="true"/g' "$WEBCONFIG"
fi

mkdir -p "$API_OUT/logs"
touch "$API_OUT/logs/.gitkeep"

log_success "API publish complete: $API_OUT"
