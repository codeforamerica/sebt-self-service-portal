#!/bin/bash
# IIS Bundle Assembler
# Combines API publish output, web zip, DACPAC, and reports into a single
# deployable zip with a generated README.
#
# Usage:
#   ./scripts/ci/bundle-iis-package.sh \
#       --api-dir <dir> \
#       --web-zip <path> \
#       --dacpac <path> \
#       --changelog <path>          # CHANGELOG-DACPAC.md
#       --deploy-report-xml <path>
#       --deploy-report-html <path>
#       --version <ver> \
#       --git-sha <sha> \
#       --output <zip>

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMPLATES_DIR="$SCRIPT_DIR/templates"
CALLER_PWD="$(pwd)"

API_DIR=""; WEB_ZIP=""; DACPAC=""; CHANGELOG=""; REPORT_XML=""; REPORT_HTML=""
VERSION=""; GIT_SHA=""; OUT_ZIP=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --api-dir) API_DIR="$2"; shift 2 ;;
    --web-zip) WEB_ZIP="$2"; shift 2 ;;
    --dacpac) DACPAC="$2"; shift 2 ;;
    --changelog) CHANGELOG="$2"; shift 2 ;;
    --deploy-report-xml) REPORT_XML="$2"; shift 2 ;;
    --deploy-report-html) REPORT_HTML="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --git-sha) GIT_SHA="$2"; shift 2 ;;
    --output) OUT_ZIP="$2"; shift 2 ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

for var in API_DIR WEB_ZIP DACPAC CHANGELOG REPORT_XML REPORT_HTML VERSION GIT_SHA OUT_ZIP; do
  if [ -z "${!var}" ]; then
    log_error "$var is required"
    exit 1
  fi
done

case "$OUT_ZIP" in
  /*) ;;
  *) OUT_ZIP="$CALLER_PWD/$OUT_ZIP" ;;
esac

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT
BUNDLE="$STAGING/sebt-dc-iis-$VERSION"
mkdir -p "$BUNDLE"

log_info "Staging API"
cp -R "$API_DIR" "$BUNDLE/api"

log_info "Staging web (extract zip and rename to web/)"
mkdir -p "$STAGING/web-extract"
unzip -q "$WEB_ZIP" -d "$STAGING/web-extract"
# The package-frontend.sh zip's root dir is `sebt-web/`. Move its contents into web/.
WEB_ROOT="$(ls -1d "$STAGING/web-extract"/*/ 2>/dev/null | head -n 1)"
if [ -z "$WEB_ROOT" ]; then
  log_error "Could not identify root dir inside web zip: $WEB_ZIP"
  exit 1
fi
mv "$WEB_ROOT" "$BUNDLE/web"
cp "$TEMPLATES_DIR/web.config" "$BUNDLE/web/web.config"
mkdir -p "$BUNDLE/web/logs"
touch "$BUNDLE/web/logs/.gitkeep"

log_info "Staging DACPAC and reports"
mkdir -p "$BUNDLE/dacpac"
cp "$DACPAC" "$BUNDLE/dacpac/"
cp "$REPORT_XML" "$BUNDLE/dacpac/deploy-report.xml"
cp "$REPORT_HTML" "$BUNDLE/dacpac/deploy-report.html"
cp "$CHANGELOG" "$BUNDLE/CHANGELOG-DACPAC.md"

log_info "Rendering README"
DACPAC_FILENAME="$(basename "$DACPAC")"
BUILD_DATE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
# Take the first ~10 non-empty lines after the title as the inline summary
DACPAC_SUMMARY="$(awk 'NR>1 && NF {print; n++; if (n>=10) exit}' "$CHANGELOG")"

# Use python for safe placeholder substitution. Pass values via env vars so
# multi-line markdown summaries with arbitrary content (including quotes) are
# handled correctly.
export TPL_VERSION="$VERSION"
export TPL_BUILD_DATE="$BUILD_DATE"
export TPL_GIT_SHA="$GIT_SHA"
export TPL_DACPAC_FILENAME="$DACPAC_FILENAME"
export TPL_DACPAC_SUMMARY="$DACPAC_SUMMARY"
python3 - "$TEMPLATES_DIR/README.iis.md.tmpl" "$BUNDLE/README.md" <<'PY'
import os, sys, pathlib
src, dst = sys.argv[1], sys.argv[2]
text = pathlib.Path(src).read_text()
subs = {
    "{{VERSION}}":         os.environ["TPL_VERSION"],
    "{{BUILD_DATE}}":      os.environ["TPL_BUILD_DATE"],
    "{{GIT_SHA}}":         os.environ["TPL_GIT_SHA"],
    "{{DACPAC_FILENAME}}": os.environ["TPL_DACPAC_FILENAME"],
    "{{DACPAC_SUMMARY}}":  os.environ["TPL_DACPAC_SUMMARY"],
}
for k, v in subs.items():
    text = text.replace(k, v)
pathlib.Path(dst).write_text(text)
PY

log_info "Zipping bundle to $OUT_ZIP"
mkdir -p "$(dirname "$OUT_ZIP")"
rm -f "$OUT_ZIP"
(cd "$STAGING" && zip -rqX "$OUT_ZIP" "sebt-dc-iis-$VERSION")

log_success "Bundle ready: $OUT_ZIP ($(du -h "$OUT_ZIP" | cut -f1))"
