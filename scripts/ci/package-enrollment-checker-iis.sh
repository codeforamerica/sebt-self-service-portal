#!/bin/bash
# Enrollment Checker IIS Packaging Script
# Bundles the Next.js static export with an IIS web.config into a deployable zip.
#
# Unlike the portal (see package-frontend.sh), the checker is a pure static
# export — no Node process, no node_modules, no symlink dereferencing. The only
# thing IIS needs beyond the exported files is a web.config supplying the
# extensionless-route rewrites and MIME maps.
#
# Usage:
#   API_BASE_URL=https://portal.example.gov PORTAL_URL=https://portal.example.gov \
#     ./scripts/ci/package-enrollment-checker-iis.sh --version <ver> [--output <zip>]
#
# Options:
#   --version <ver>   Version label for the bundle (required)
#   --output <path>   Output zip path (default: output/sebt-enrollment-checker-dc-iis-<ver>.zip)
#   --out-dir <path>  Static export directory (default: the checker's out/)
#
# Environment config:
#   Browser config is NOT inlined at build. The export is environment-neutral and
#   carries a config.js that assigns window.__CHECKER_CONFIG__, loaded from <head>
#   before the app bundle. This script regenerates that file from the environment
#   via write-checker-config.sh, so the same export can be packaged for each
#   environment by re-running with different values. See ADR 0023.
#
#   Recognized variables, all optional and all passed straight through:
#     API_BASE_URL, PORTAL_URL, APPLICATION_URL
#     AMPLITUDE_API_KEY, MIXPANEL_TOKEN, SITEIMPROVE_ID
#     META_PIXEL, META_PIXEL_ACTION
#     ADENTIFI_PIXEL_LANDING, ADENTIFI_PIXEL_APPLY_NOW
#     SHOW_SCHOOL_FIELD, CHECKER_ENABLED, BOT_PROTECTION_ENABLED
#
#   A blank variable is left out of config.js, which falls the checker back to the
#   value baked in at build. Clearing a value therefore has to happen at build
#   time, not here — notably APPLICATION_URL, which governs the next-season apply
#   link.
#
# Prerequisites:
#   The checker must already be built with BUILD_STATIC=true, and its SSR-only
#   route handlers under src/app/api removed first: output:'export' cannot emit
#   them and the build fails collecting page data. STATE and BASE_PATH select
#   build-time output, so they are fixed in the export and cannot be changed here.

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMPLATES_DIR="$SCRIPT_DIR/templates"
CALLER_PWD="$(pwd)"

CHECKER_DIR="$PROJECT_ROOT/apps/portal/src/SEBT.EnrollmentChecker.Web"
OUT_DIR="$CHECKER_DIR/out"
VERSION=""
OUT_ZIP=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    --output) OUT_ZIP="$2"; shift 2 ;;
    --out-dir) OUT_DIR="$2"; shift 2 ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$VERSION" ]; then
  log_error "--version is required"
  exit 1
fi

if [[ ! "$VERSION" =~ ^[A-Za-z0-9._-]+$ ]]; then
  log_error "Invalid version: $VERSION"
  exit 1
fi

OUT_ZIP="${OUT_ZIP:-output/sebt-enrollment-checker-dc-iis-$VERSION.zip}"
case "$OUT_ZIP" in
  /*) ;;
  *) OUT_ZIP="$CALLER_PWD/$OUT_ZIP" ;;
esac

if [ ! -d "$OUT_DIR" ]; then
  log_error "No static export found at $OUT_DIR"
  log_error "Build it first with BUILD_STATIC=true pnpm --filter @sebt/enrollment-checker build"
  exit 1
fi

if [ ! -f "$OUT_DIR/index.html" ]; then
  log_error "$OUT_DIR has no index.html — not a completed static export"
  exit 1
fi

WEB_CONFIG="$TEMPLATES_DIR/web.enrollment-checker.config"
if [ ! -f "$WEB_CONFIG" ]; then
  log_error "Missing IIS template: $WEB_CONFIG"
  exit 1
fi

WRITE_CONFIG="$SCRIPT_DIR/write-checker-config.sh"
if [ ! -f "$WRITE_CONFIG" ]; then
  log_error "Missing config writer: $WRITE_CONFIG"
  exit 1
fi

# STATE and BASE_PATH select build-time output and cannot be changed here, so a
# wrong one means the whole export is wrong. Every other value now comes from
# config.js, which this script regenerates.
report_build_inputs() {
  log_info "Fixed at build time (cannot be changed by this script):"
  grep -rhoE 'NEXT_PUBLIC_(STATE|BASE_PATH)"?:"[^"]*"' \
    "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null | sort -u | sed 's/^/     /' || true

  # An absolute-rooted export cannot be served from a virtual directory.
  if grep -rqE 'NEXT_PUBLIC_BASE_PATH"?:""' "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null; then
    log_info "BASE_PATH is empty — this bundle must be served from a site ROOT, not a virtual directory."
  fi

  # A build that still carries an apply URL resurfaces the next-season apply link
  # wherever config.js omits applicationUrl, and no config.js can clear it.
  if grep -rqE 'NEXT_PUBLIC_APPLICATION_URL"?:"https?://' "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null; then
    log_warning "The export has a build-time APPLICATION_URL fallback. The apply link will show unless config.js overrides it."
  fi
}

# A trailing slash yields a double slash once the client appends its path, which
# stops matching both the portal's CORS check and its API proxy route.
check_api_base_url() {
  case "${API_BASE_URL:-}" in
    */) log_error "API_BASE_URL ends in '/' — requests would contain '//api/...' and fail."
        exit 1 ;;
  esac
}

write_runtime_config() {
  local site_dir="$1"
  log_info "Writing config.js for this environment..."
  bash "$WRITE_CONFIG" "$site_dir/config.js"
  log_info "config.js contents:"
  grep -v '^\s*//' "$site_dir/config.js" | sed 's/^/     /'
}

package() {
  STAGING_DIR=$(mktemp -d)
  trap 'rm -rf "$STAGING_DIR"' EXIT

  local SITE_DIR="$STAGING_DIR/site"
  mkdir -p "$SITE_DIR"

  log_info "Staging static export..."
  cp -R "$OUT_DIR"/. "$SITE_DIR"/

  cp "$WEB_CONFIG" "$SITE_DIR/web.config"
  log_success "web.config added"

  # Overwrites the empty placeholder copied from public/.
  write_runtime_config "$SITE_DIR"

  # Strip macOS metadata that confuses Windows tooling.
  find "$SITE_DIR" -name '.DS_Store' -delete 2>/dev/null || true
  find "$SITE_DIR" -name '._*' -delete 2>/dev/null || true

  mkdir -p "$(dirname "$OUT_ZIP")"
  rm -f "$OUT_ZIP"

  log_info "Creating zip archive..."
  (cd "$STAGING_DIR" && COPYFILE_DISABLE=1 zip -rqX "$OUT_ZIP" site/)
  log_success "Archive created: $OUT_ZIP"

  log_info "Archive size: $(du -sh "$OUT_ZIP" | cut -f1)"
  log_info "Files: $(find "$SITE_DIR" -type f | wc -l | tr -d ' ')"
}

main() {
  log_info "=== Enrollment Checker IIS Packaging ==="
  log_info "Export:  $OUT_DIR"
  log_info "Version: $VERSION"
  echo ""

  check_api_base_url
  report_build_inputs
  echo ""
  package

  echo ""
  log_success "=== Packaging complete ==="
  log_info "To deploy on the IIS host:"
  log_info "  1. Extract the zip; copy site/ to the IIS physical path"
  log_info "  2. Point an IIS site (not a virtual directory) at that folder"
  log_info "  3. Install the URL Rewrite module if not already present"
  log_info "  4. Set ENROLLMENT_CHECKER_ORIGIN on the PORTAL site to this site's origin,"
  log_info "     otherwise the portal will not return CORS headers to the checker"
}

main "$@"
