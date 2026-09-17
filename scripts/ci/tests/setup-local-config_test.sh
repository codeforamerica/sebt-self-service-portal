#!/usr/bin/env bash
# Smoke tests for scripts/dev/setup-local-config.sh, run against a temporary copy of the real
# example config files so they break when an example file's placeholders change.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

SCRIPT="$PROJECT_ROOT/scripts/dev/setup-local-config.sh"
API=apps/portal/src/SEBT.Portal.Api
WEB=apps/portal/src/SEBT.Portal.Web
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# A fresh checkout: only the example files exist.
new_checkout() {
  rm -rf "$WORK/root"
  mkdir -p "$WORK/root/$API" "$WORK/root/$WEB"
  cp "$PROJECT_ROOT/.env.example" "$WORK/root/.env.example"
  cp "$PROJECT_ROOT/$WEB/.env.example" "$WORK/root/$WEB/.env.example"
  for name in Development dc co; do
    cp "$PROJECT_ROOT/$API/appsettings.$name.example.json" "$WORK/root/$API/"
  done
}

json_value() {
  node -e 'const j = require(process.argv[1]); console.log(process.argv[2].split(".").reduce((o, k) => o[k], j))' "$1" "$2"
}

assert_not_contains() {
  if grep -qF -- "$2" "$1"; then
    echo "ASSERT FAIL: expected file '$1' to NOT contain: $2" >&2
    exit 1
  fi
}

# --- Test 1: a fresh checkout gets every config file, with local values filled in ---
echo "[setup-local-config_test] case 1: creates and fills config files"
new_checkout
bash "$SCRIPT" --root "$WORK/root" > "$WORK/out.txt"
DEV="$WORK/root/$API/appsettings.Development.json"
assert_file_exists "$WORK/root/.env"
assert_file_exists "$WORK/root/$WEB/.env.local"
assert_file_exists "$DEV"
assert_file_exists "$WORK/root/$API/appsettings.dc.json"
assert_file_exists "$WORK/root/$API/appsettings.co.json"
# The database password comes from .env, so compose and the API agree.
db_password="$(grep '^MSSQL_SA_PASSWORD=' "$WORK/root/.env" | cut -d= -f2-)"
assert_contains "$DEV" "Password=$db_password;"
assert_not_contains "$DEV" "YOUR_PASSWORD"
assert_not_contains "$WORK/root/$API/appsettings.dc.json" "YOUR_PASSWORD"
assert_not_contains "$DEV" "YOUR_JWT_SECRET_AT_LEAST_32_CHARS"
assert_not_contains "$DEV" "YOUR_IDENTIFIER_HASHER_KEY_AT_LEAST_32_CHARS"
assert_not_contains "$DEV" "AT_LEAST_32_CHARACTERS_FOR_HMAC_SHA256_SIGNING"
# Values only the developer can supply are reported, not invented.
assert_contains "$WORK/out.txt" "YOUR_CBMS_CLIENT_ID"
assert_contains "$DEV" "YOUR_CBMS_CLIENT_ID"
echo "[setup-local-config_test] case 1: OK"

# --- Test 2: generated secrets are long enough for the startup validators and not reused ---
echo "[setup-local-config_test] case 2: generates distinct secrets"
jwt="$(json_value "$DEV" JwtSettings.SecretKey)"
hasher="$(json_value "$DEV" IdentifierHasher.SecretKey)"
signing="$(json_value "$DEV" Oidc.CompleteLoginSigningKey)"
for key in "$jwt" "$hasher" "$signing"; do
  if [ "${#key}" -lt 32 ]; then
    echo "ASSERT FAIL: generated key shorter than 32 characters: $key" >&2
    exit 1
  fi
done
if [ "$jwt" = "$hasher" ] || [ "$jwt" = "$signing" ] || [ "$hasher" = "$signing" ]; then
  echo "ASSERT FAIL: generated keys must differ from one another" >&2
  exit 1
fi
echo "[setup-local-config_test] case 2: OK"

# --- Test 3: re-running never overwrites a file the developer already has ---
echo "[setup-local-config_test] case 3: keeps existing files"
echo "LOCAL_EDIT=kept" >> "$WORK/root/.env"
bash "$SCRIPT" --root "$WORK/root" > "$WORK/out.txt"
assert_contains "$WORK/root/.env" "LOCAL_EDIT=kept"
assert_eq "$(json_value "$DEV" JwtSettings.SecretKey)" "$jwt"
echo "[setup-local-config_test] case 3: OK"

# --- Test 4: --state limits the state overlay files to the chosen state ---
echo "[setup-local-config_test] case 4: --state co skips the DC overlay"
new_checkout
bash "$SCRIPT" --root "$WORK/root" --state co > "$WORK/out.txt"
assert_file_exists "$WORK/root/$API/appsettings.co.json"
if [ -f "$WORK/root/$API/appsettings.dc.json" ]; then
  echo "ASSERT FAIL: appsettings.dc.json should not be created for --state co" >&2
  exit 1
fi
echo "[setup-local-config_test] case 4: OK"

# --- Test 5: an unknown state fails instead of silently doing nothing ---
echo "[setup-local-config_test] case 5: rejects an unknown state"
new_checkout
if bash "$SCRIPT" --root "$WORK/root" --state tx > "$WORK/out.txt" 2>&1; then
  echo "ASSERT FAIL: expected --state tx to fail" >&2
  exit 1
fi
echo "[setup-local-config_test] case 5: OK"

# --- Test 6: an existing .env without a database password falls back to .env.example's ---
echo "[setup-local-config_test] case 6: falls back to the example's database password"
new_checkout
sed -i.bak 's/^MSSQL_SA_PASSWORD=.*/MSSQL_SA_PASSWORD=Example-Only-Pa55word/' "$WORK/root/.env.example"
grep -v '^MSSQL_SA_PASSWORD=' "$WORK/root/.env.example" > "$WORK/root/.env"
bash "$SCRIPT" --root "$WORK/root" --state co > "$WORK/out.txt"
assert_contains "$WORK/root/$API/appsettings.Development.json" "Password=Example-Only-Pa55word;"
echo "[setup-local-config_test] case 6: OK"

# --- Test 7: --root without a directory shows usage instead of an unbound-variable crash ---
echo "[setup-local-config_test] case 7: --root needs a value"
set +e
bash "$SCRIPT" --root > "$WORK/out.txt" 2>&1
status=$?
set -e
assert_eq "$status" "2"
assert_contains "$WORK/out.txt" "Usage:"
assert_not_contains "$WORK/out.txt" "unbound variable"
echo "[setup-local-config_test] case 7: OK"

echo "setup-local-config_test: OK"
