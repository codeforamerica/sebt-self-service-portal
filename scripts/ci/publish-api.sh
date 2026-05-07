#!/bin/bash
# API Publish Script
# Publishes the .NET API for win-x64 (framework-dependent) into <output>/api/,
# emits a secrets-only appsettings.prod.example.json, and patches the
# auto-generated web.config to enable ASP.NET Core stdout logging.
#
# Plugin DLLs are NOT copied by this script — they must already be in
# src/SEBT.Portal.Api/plugins-dc/ before this runs. In CI, the release workflow
# stages them explicitly by publishing the DC connector with CopyPlugins=false
# and copying its publish/*.dll output into plugins-dc/. The API csproj's
# <None Include="plugins-dc\**\*.dll"> ItemGroup then picks them up during publish.
# If the directory is empty, this script fails fast rather than producing an
# invalid DC IIS bundle that will fail during MEF composition at runtime.
#
# Usage:
#   ./scripts/ci/publish-api.sh --output <dir> [--configuration Release]
#
# Options:
#   --output <dir>            Where to place the api/ directory (required).
#   --configuration <cfg>     Debug or Release (default Release).

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CONFIGURATION="Release"
OUTPUT_DIR=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --output) OUTPUT_DIR="$2"; shift 2 ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
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

# Sanity-check that DC plugin DLLs have already been staged into plugins-dc/.
PLUGIN_DIR="$PROJECT_ROOT/src/SEBT.Portal.Api/plugins-dc"
if [ -z "$(ls -A "$PLUGIN_DIR" 2>/dev/null | grep -E '\.dll$' || true)" ]; then
  log_error "$PLUGIN_DIR has no DLLs — DC plugin artifacts were not staged before publish-api.sh."
  log_error "Stage the DC connector publish output before building the IIS bundle."
  exit 1
fi

API_OUT="$OUTPUT_DIR/api"
mkdir -p "$API_OUT"

log_info "Publishing API to $API_OUT (configuration: $CONFIGURATION, runtime: win-x64)"
dotnet publish "$PROJECT_ROOT/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj" \
  --configuration "$CONFIGURATION" \
  --runtime win-x64 \
  --self-contained false \
  --output "$API_OUT" \
  -p:BuildFrontend=false \
  --verbosity minimal

log_info "Writing appsettings.prod.example.json (DC-specific / secret keys only)"
cat > "$API_OUT/appsettings.prod.example.json" <<'JSON'
{
  "_comment": "Copy this file to appsettings.Production.json and fill in DC production values. Defaults for everything not listed here come from appsettings.json. Do not commit your filled-in copy. Every key with a YOUR_* placeholder MUST be replaced before the API will run correctly.",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_DB_HOST,1433;Database=SEBT_Portal_DC;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
  },
  "DCConnector": {
    "ConnectionString": "Server=YOUR_DC_SOURCE_DB_HOST,1433;Database=DcSource;User Id=YOUR_DC_SOURCE_USER;Password=YOUR_DC_SOURCE_PASSWORD;TrustServerCertificate=True;"
  },
  "IdentifierHasher": {
    "_comment": "32+ character random secret used to hash identifiers (email, phone, etc) when stored. Generate with: openssl rand -base64 48",
    "SecretKey": "YOUR_IDENTIFIER_HASHER_SECRET_KEY_AT_LEAST_32_CHARACTERS"
  },
  "JwtSettings": {
    "_comment": "32+ character HMAC secret for signing API JWTs. Generate with: openssl rand -base64 48",
    "SecretKey": "YOUR_JWT_HMAC_SECRET_KEY_AT_LEAST_32_CHARACTERS"
  },
  "Oidc": {
    "_comment": "32+ character HMAC secret for signing the OIDC complete-login token.",
    "CompleteLoginSigningKey": "YOUR_OIDC_COMPLETE_LOGIN_SIGNING_KEY_AT_LEAST_32_CHARACTERS"
  },
  "SmtpClientSettings": {
    "_comment": "Defaults in appsettings.json point at Mailpit (localhost:1025) for local dev. Override for production with the agency's outbound SMTP relay.",
    "SmtpServer": "YOUR_SMTP_HOST",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "YOUR_SMTP_USER",
    "Password": "YOUR_SMTP_PASSWORD"
  },
  "AppConfig": {
    "_comment": "AWS AppConfig integration. If you are not using AWS AppConfig, leave the IDs blank and ensure the Enabled flags in FeatureManagement.AppConfig are also false.",
    "Agent": {
      "ApplicationId": "YOUR_APPCONFIG_APPLICATION_ID_OR_EMPTY",
      "EnvironmentId": "YOUR_APPCONFIG_ENVIRONMENT_ID_OR_EMPTY"
    },
    "FeatureFlags": {
      "ProfileId": "YOUR_APPCONFIG_FEATURE_FLAGS_PROFILE_ID_OR_EMPTY"
    },
    "AppSettings": {
      "ProfileId": "YOUR_APPCONFIG_APPSETTINGS_PROFILE_ID_OR_EMPTY"
    }
  },
  "Smarty": {
    "_comment": "SmartyStreets address validation. Leave Enabled=false to skip; set Enabled=true and provide credentials to use.",
    "Enabled": true,
    "AuthId": "YOUR_SMARTY_AUTH_ID",
    "AuthToken": "YOUR_SMARTY_AUTH_TOKEN"
  },
  "Socure": {
    "_comment": "Socure identity verification. ApiKey and WebhookSecret are obtained from the Socure RiskOS dashboard.",
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
