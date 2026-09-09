#!/bin/bash
# Resolves what commit SHA a live deployment is actually running, for release-notes
# diffing (see generate.ts --since-sha). Fetches <url>/api/build-info, extracts the
# requested field, and resolves it to a full commit SHA via `git rev-parse --verify`
# — this accepts both DC's full-length and CO's short-form SHA uniformly, and fails
# loudly (non-zero exit) on any error rather than silently falling back to a guess.
#
# Usage:
#   ./scripts/release-notes/resolve-live-sha.sh \
#       --url <prod-url> \
#       --field <buildSha|dcConnectorSha> \
#       [--git-dir <dir>]   (default: .)
#
# Prints the resolved, full 40-char commit SHA to stdout on success. All logging
# goes to stderr, so stdout is safe to capture directly, e.g.:
#   OLD_SHA=$(./scripts/release-notes/resolve-live-sha.sh --url "$PROD_URL" --field buildSha)

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}" >&2; }
log_success() { echo -e "${GREEN}✅ $1${NC}" >&2; }
log_error()   { echo -e "${RED}❌ $1${NC}" >&2; }

URL=""
FIELD=""
GIT_DIR="."

while [ $# -gt 0 ]; do
  case "$1" in
    --url) URL="$2"; shift 2 ;;
    --field) FIELD="$2"; shift 2 ;;
    --git-dir) GIT_DIR="$2"; shift 2 ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$URL" ] || [ -z "$FIELD" ]; then
  log_error "--url and --field are required"
  exit 1
fi
if [ "$FIELD" != "buildSha" ] && [ "$FIELD" != "dcConnectorSha" ]; then
  log_error "--field must be buildSha or dcConnectorSha (got: $FIELD)"
  exit 1
fi

BUILD_INFO_URL="${URL%/}/api/build-info"
log_info "Fetching $BUILD_INFO_URL"

RESPONSE="$(curl --fail --silent --show-error --max-time 15 "$BUILD_INFO_URL")" || {
  log_error "Failed to fetch $BUILD_INFO_URL"
  exit 1
}

# --fail only catches HTTP-level errors. Some environments return 200 with a
# non-JSON body (e.g. a catch-all redirect target) for a route that doesn't
# exist — confirmed against real CO infra, not a hypothetical. Validate the
# response is actually JSON before trying to extract a field from it, so the
# failure is a clear message instead of a raw jq parse error.
if ! echo "$RESPONSE" | jq -e . >/dev/null 2>&1; then
  log_error "Response from $BUILD_INFO_URL was not valid JSON: $RESPONSE"
  exit 1
fi

SHA="$(echo "$RESPONSE" | jq -r --arg field "$FIELD" '.[$field] // empty')"

if [ -z "$SHA" ]; then
  log_error "Field '$FIELD' was missing or null in the response from $BUILD_INFO_URL: $RESPONSE"
  exit 1
fi

log_info "Got $FIELD=$SHA from $BUILD_INFO_URL — resolving against $GIT_DIR"

if ! RESOLVED="$(git -C "$GIT_DIR" rev-parse --verify "${SHA}^{commit}" 2>&1)"; then
  log_error "Could not resolve '$SHA' to a commit in $GIT_DIR (ambiguous or unknown revision): $RESOLVED"
  exit 1
fi

log_success "Resolved $FIELD $SHA -> $RESOLVED"
echo "$RESOLVED"
