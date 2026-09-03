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
#   ./scripts/ci/package-enrollment-checker-iis.sh --version <ver> [--output <zip>]
#
# Options:
#   --version <ver>   Version label for the bundle (required)
#   --output <path>   Output zip path (default: output/sebt-enrollment-checker-dc-iis-<ver>.zip)
#   --out-dir <path>  Static export directory (default: the checker's out/)
#
# Prerequisites:
#   The checker must already be built with BUILD_STATIC=true. Because
#   NEXT_PUBLIC_* values are inlined at build time, this script cannot change
#   them — verify them in the export before packaging.

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

# NEXT_PUBLIC_* values are frozen into the JS chunks at build time, so this is
# the last point at which a wrong environment can be caught. Print them rather
# than validate: the correct values differ per environment.
report_baked_values() {
  log_info "Baked-in client configuration (cannot be changed after build):"
  grep -rhoE 'NEXT_PUBLIC_(STATE|BASE_PATH|API_BASE_URL|PORTAL_URL|APPLICATION_URL)"?:"[^"]*"' \
    "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null | sort -u | sed 's/^/     /' || true

  # A trailing slash yields a double slash once the client appends its path,
  # which stops matching both the portal's CORS check and its API proxy route.
  if grep -rqE 'NEXT_PUBLIC_API_BASE_URL"?:"[^"]*/"' "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null; then
    log_warning "NEXT_PUBLIC_API_BASE_URL ends in '/' — requests will contain '//api/...' and fail. Rebuild without it."
  fi

  # An absolute-rooted export cannot be served from a virtual directory.
  if grep -rqE 'NEXT_PUBLIC_BASE_PATH"?:""' "$OUT_DIR"/_next/static/chunks/*.js 2>/dev/null; then
    log_info "BASE_PATH is empty — this bundle must be served from a site ROOT, not a virtual directory."
  fi
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

  report_baked_values
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
