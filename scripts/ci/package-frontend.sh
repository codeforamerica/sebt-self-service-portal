#!/bin/bash
# Frontend Packaging Script
# Produces a portable, Windows-compatible zip of the Next.js standalone build.
#
# The Next.js standalone output uses pnpm symlinks that break when transferred
# to another machine (especially Windows, which also has a 260-char path limit).
# This script dereferences symlinks, hoists the .pnpm store to flat node_modules,
# strips macOS metadata, and produces a clean zip archive.
#
# Usage:
#   ./scripts/ci/package-frontend.sh [--output <path>] [--skip-build]
#
# Options:
#   --output <path>   Output zip path (default: output/sebt-web.zip)
#   --skip-build      Skip the build step (use existing .next/standalone)
#
# Prerequisites:
#   The frontend must be built with BUILD_STANDALONE=true before packaging,
#   or omit --skip-build to let this script build it.

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
FRONTEND_DIR="$PROJECT_ROOT/src/SEBT.Portal.Web"
STANDALONE_DIR="$FRONTEND_DIR/.next/standalone"

# Defaults
OUTPUT_ZIP="$PROJECT_ROOT/output/sebt-web.zip"
SKIP_BUILD=false

for arg in "$@"; do
  case $arg in
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --output)
      shift
      OUTPUT_ZIP="$1"
      shift
      ;;
  esac
done

case "$OUTPUT_ZIP" in
  /*) ;;
  *) OUTPUT_ZIP="$PROJECT_ROOT/$OUTPUT_ZIP" ;;
esac

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

remove_dangling_symlinks() {
  local dangling_links_file
  dangling_links_file=$(mktemp)

  find "$STANDALONE_DIR" -type l ! -exec test -e {} \; -print > "$dangling_links_file"

  if [ -s "$dangling_links_file" ]; then
    log_warning "Removing dangling symlinks from standalone output before packaging..."
    while IFS= read -r dangling_link; do
      [ -n "$dangling_link" ] || continue
      log_warning "  $(basename "$dangling_link")"
      rm "$dangling_link"
    done < "$dangling_links_file"
  fi

  rm -f "$dangling_links_file"
}

build_standalone() {
  if [ "$SKIP_BUILD" = true ]; then
    log_info "Skipping build (--skip-build)"
    if [ ! -d "$STANDALONE_DIR" ]; then
      log_error "No standalone build found at $STANDALONE_DIR"
      log_error "Run the build first: BUILD_STANDALONE=true pnpm --filter @sebt/web build"
      exit 1
    fi
    return 0
  fi

  log_info "Building frontend with standalone output..."
  cd "$FRONTEND_DIR"
  BUILD_STANDALONE=true pnpm build
  log_success "Build complete"
}

package_frontend() {
  STAGING_DIR=$(mktemp -d)
  local WEB_DIR="$STAGING_DIR/sebt-web"

  trap 'rm -rf "$STAGING_DIR"' EXIT

  log_info "Staging portable build..."

  remove_dangling_symlinks

  # Copy the entire standalone output, dereferencing pnpm symlinks so the
  # result contains real files that work on any OS without pnpm installed.
  # rsync handles deep symlink chains more reliably than macOS `cp -rL`.
  mkdir -p "$WEB_DIR"
  rsync -aL "$STANDALONE_DIR/" "$WEB_DIR/"

  # The standalone output nests the app under src/SEBT.Portal.Web/ with a
  # top-level node_modules/.pnpm virtual store. Hoist all packages from
  # .pnpm/*/node_modules/* into a flat node_modules/ layout so Node.js
  # module resolution works without pnpm's symlink structure.
  local PNPM_STORE="$WEB_DIR/node_modules/.pnpm"
  if [ -d "$PNPM_STORE" ]; then
    log_info "Hoisting pnpm virtual store to flat node_modules..."
    shopt -s nullglob
    for pkg_dir in "$PNPM_STORE"/*/node_modules/*; do
      local pkg_name
      pkg_name=$(basename "$pkg_dir")
      if [ "$pkg_name" != ".pnpm" ] && [ ! -e "$WEB_DIR/node_modules/$pkg_name" ]; then
        cp -r "$pkg_dir" "$WEB_DIR/node_modules/$pkg_name"
      fi
    done

    # Handle scoped packages (@org/pkg) which are one level deeper
    for pkg_dir in "$PNPM_STORE"/*/node_modules/@*/*; do
      if [ -d "$pkg_dir" ]; then
        local scope_name
        scope_name=$(basename "$(dirname "$pkg_dir")")
        local pkg_name
        pkg_name=$(basename "$pkg_dir")
        local target_dir="$WEB_DIR/node_modules/$scope_name"
        mkdir -p "$target_dir"
        if [ ! -e "$target_dir/$pkg_name" ]; then
          cp -r "$pkg_dir" "$target_dir/$pkg_name"
        fi
      fi
    done
    shopt -u nullglob

    rm -rf "$PNPM_STORE"
    log_success "Dependencies hoisted"
  fi

  # Copy static assets (CSS, JS bundles) and public files (favicon, images)
  # into the location server.js expects them.
  local APP_DIR="$WEB_DIR/src/SEBT.Portal.Web"
  if [ -d "$APP_DIR" ]; then
    mkdir -p "$APP_DIR/.next"
    cp -r "$FRONTEND_DIR/.next/static" "$APP_DIR/.next/static"
    cp -r "$FRONTEND_DIR/public" "$APP_DIR/public"
    log_success "Static assets and public files copied"
  else
    log_error "Expected app directory not found: $APP_DIR"
    exit 1
  fi

  # Strip macOS metadata that causes issues on Windows
  find "$WEB_DIR" -name '.DS_Store' -delete 2>/dev/null || true
  find "$WEB_DIR" -name '.__*' -delete 2>/dev/null || true

  # Create the output directory
  mkdir -p "$(dirname "$OUTPUT_ZIP")"

  # Remove old zip if it exists
  rm -f "$OUTPUT_ZIP"

  # Create a clean zip without macOS resource forks
  log_info "Creating zip archive..."
  (cd "$STAGING_DIR" && COPYFILE_DISABLE=1 zip -rqX "$OUTPUT_ZIP" sebt-web/)
  log_success "Archive created: $OUTPUT_ZIP"

  local ZIP_SIZE
  ZIP_SIZE=$(du -sh "$OUTPUT_ZIP" | cut -f1)
  log_info "Archive size: $ZIP_SIZE"
}

main() {
  log_info "=== Frontend Packaging Script ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Output: $OUTPUT_ZIP"
  echo ""

  build_standalone
  package_frontend

  echo ""
  log_success "=== Packaging complete ==="
  log_info "To deploy on the target machine:"
  log_info "  1. Extract sebt-web.zip"
  log_info "  2. cd sebt-web/src/SEBT.Portal.Web"
  log_info "  3. node server.js"
  log_info ""
  log_info "Environment variables:"
  log_info "  PORT            - HTTP port (default: 3000)"
  log_info "  HOSTNAME        - Bind address (default: 0.0.0.0)"
  log_info "  BACKEND_URL     - .NET API URL (default: http://localhost:5280)"
}

main "$@"
