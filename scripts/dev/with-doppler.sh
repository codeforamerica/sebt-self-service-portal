#!/bin/bash
# Runs a command under `doppler run` when this repo is set up for Doppler, otherwise runs it directly.
# USE_DOPPLER: unset = auto-detect, true/1 = require Doppler, false/0 = never use Doppler.

set -e  # Exit on error
set -u  # Exit on undefined variable

# Colors for output
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

EXPECTED_PROJECT="safety-net-sebt-self-service-portal"

log_info() {
  echo -e "${BLUE}ℹ️  $1${NC}"
}

log_error() {
  echo -e "${RED}❌ $1${NC}" >&2
}

use_doppler="$(printf '%s' "${USE_DOPPLER:-}" | tr '[:upper:]' '[:lower:]')"

case "$use_doppler" in
  ""|true|1|false|0) ;;
  *)
    log_error "USE_DOPPLER must be true, false, 1 or 0 (or unset to auto-detect), but was '${USE_DOPPLER}'."
    exit 1
    ;;
esac

if [ "$use_doppler" = "false" ] || [ "$use_doppler" = "0" ]; then
  log_info "USE_DOPPLER=${USE_DOPPLER}: using local config files"
  exec "$@"
fi

configured_project=""
if command -v doppler >/dev/null 2>&1; then
  configured_project="$(doppler configure get project --plain 2>/dev/null || true)"
fi

if [ "$configured_project" != "$EXPECTED_PROJECT" ]; then
  if [ "$use_doppler" = "true" ] || [ "$use_doppler" = "1" ]; then
    log_error "USE_DOPPLER=${USE_DOPPLER}, but Doppler isn't set up for this repo. Install the Doppler CLI, run 'doppler login', then run 'doppler setup --no-interactive' from the repo root."
    exit 1
  fi
  log_info "Doppler isn't set up for this repo: using local config files"
  exec "$@"
fi

# Load secrets up front so a Doppler failure stops here instead of starting the app with placeholder secrets.
if ! doppler_error="$(doppler run -- true 2>&1 >/dev/null)"; then
  log_error "Doppler couldn't load secrets:"
  echo "$doppler_error" >&2
  log_error "Fix the Doppler error above, or set USE_DOPPLER=false to use local config files."
  exit 1
fi

log_info "Using Doppler: ${EXPECTED_PROJECT} / $(doppler configure get config --plain) (Doppler values override local appsettings and .env.local)"
exec doppler run -- "$@"
