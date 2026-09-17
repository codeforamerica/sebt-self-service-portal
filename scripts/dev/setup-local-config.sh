#!/usr/bin/env bash
# Local Config Setup
#
# Creates the gitignored config files a local run needs from their checked-in examples, and fills
# in the values that can be produced on this machine.
#
# Usage:
#   ./scripts/dev/setup-local-config.sh [--state dc|co]... [--root DIR]
#
# Without --state, sets up both states. --root points at another checkout (the smoke test uses it).
#
# A file that already exists is never touched, so a re-run is safe and keeps local edits. In each
# file it creates, it fills in:
#   YOUR_PASSWORD                            MSSQL_SA_PASSWORD from .env, so compose and the API agree
#   JWT, identifier hasher and OIDC signing  a fresh random 64-character key each
# Values only a developer can supply, such as client IDs and secrets, stay as placeholders and are
# listed at the end.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
STATES=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --root)
      ROOT="$2"
      shift 2
      ;;
    --state)
      case "${2:-}" in
        dc | co) STATES="$STATES $2" ;;
        *)
          echo "❌ Unknown state: ${2:-} (expected dc or co)" >&2
          exit 2
          ;;
      esac
      shift 2
      ;;
    *)
      echo "Usage: $0 [--state dc|co]... [--root DIR]" >&2
      exit 2
      ;;
  esac
done
STATES="${STATES:-dc co}"

API="$ROOT/apps/portal/src/SEBT.Portal.Api"
WEB="$ROOT/apps/portal/src/SEBT.Portal.Web"
# Compose falls back to this password when .env does not set one.
COMPOSE_DEFAULT_DB_PASSWORD="YourStrong@Passw0rd"

# Copies an example to its real name unless that file already exists. Succeeds only when it created
# the file, so callers fill in values on new files and never on a developer's own.
copy_example() {
  local example="$1" target="$2"
  if [ -f "$target" ]; then
    echo "ℹ️  Keeping existing ${target#"$ROOT"/}"
    return 1
  fi
  cp "$example" "$target"
  echo "✅ Created ${target#"$ROOT"/}"
}

# Replaces a literal placeholder. The value travels through the environment rather than the
# pattern, so characters such as & or / in a password are written as-is.
replace_placeholder() {
  PLACEHOLDER="$2" VALUE="$3" perl -pi -e 's/\Q$ENV{PLACEHOLDER}\E/$ENV{VALUE}/g' "$1"
}

copy_example "$ROOT/.env.example" "$ROOT/.env" || true
copy_example "$WEB/.env.example" "$WEB/.env.local" || true

db_password="$(grep '^MSSQL_SA_PASSWORD=' "$ROOT/.env" | cut -d= -f2- || true)"
db_password="${db_password:-$COMPOSE_DEFAULT_DB_PASSWORD}"

api_settings=""
for name in Development $STATES; do
  target="$API/appsettings.$name.json"
  api_settings="$api_settings $target"
  if copy_example "$API/appsettings.$name.example.json" "$target"; then
    replace_placeholder "$target" YOUR_PASSWORD "$db_password"
    replace_placeholder "$target" YOUR_JWT_SECRET_AT_LEAST_32_CHARS "$(openssl rand -hex 32)"
    replace_placeholder "$target" YOUR_IDENTIFIER_HASHER_KEY_AT_LEAST_32_CHARS "$(openssl rand -hex 32)"
    replace_placeholder "$target" AT_LEAST_32_CHARACTERS_FOR_HMAC_SHA256_SIGNING "$(openssl rand -hex 32)"
  fi
done

# shellcheck disable=SC2086 # api_settings is a space-separated list of paths without spaces
remaining="$(grep -HnoE 'YOUR_[A-Z0-9_]+|SECRET_STORE_[A-Z_]+' $api_settings || true)"
if [ -n "$remaining" ]; then
  echo ""
  echo "⚠️  Placeholders still to fill in, only if you need these integrations locally:"
  while IFS= read -r line; do
    echo "   ${line#"$ROOT"/}"
  done <<< "$remaining"
fi
