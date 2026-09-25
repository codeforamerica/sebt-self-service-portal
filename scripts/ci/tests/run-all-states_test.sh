#!/usr/bin/env bash
# Smoke tests for scripts/dev/run-all-states.sh. --dry-run prints the commands each state would run
# without building or starting anything, so the port, database and build-directory assignments can
# be checked here.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

SCRIPT="$PROJECT_ROOT/scripts/dev/run-all-states.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

assert_not_contains() {
  if grep -qF -- "$2" "$1"; then
    echo "ASSERT FAIL: expected file '$1' to NOT contain: $2" >&2
    exit 1
  fi
}

# Prints the dry-run line for one process, e.g. "co:api".
process_line() {
  grep -F "$1 " "$WORK/out.txt"
}

cat > "$WORK/.env" <<'ENV'
MSSQL_SA_PASSWORD=Local-Only-Pa55word
MSSQL_PORT=14330
MSSQL_USER=sa
ENV

# --- Test 1: each state gets its own ports, database, and build directories ---
echo "[run-all-states_test] case 1: isolates each state"
bash "$SCRIPT" --dry-run --root "$WORK" > "$WORK/out.txt"

co_api="$(process_line co:api)"
dc_api="$(process_line dc:api)"
co_web="$(process_line co:web)"
dc_web="$(process_line dc:web)"
for line in "$co_api" "$dc_api" "$co_web" "$dc_web"; do
  [ -n "$line" ] || { echo "ASSERT FAIL: missing a process line in dry-run output" >&2; cat "$WORK/out.txt" >&2; exit 1; }
done

echo "$co_api" > "$WORK/co-api.txt"
assert_contains "$WORK/co-api.txt" "STATE=co"
assert_contains "$WORK/co-api.txt" "ASPNETCORE_URLS=http://localhost:5280"
assert_contains "$WORK/co-api.txt" "Database=SebtPortal_co"
assert_contains "$WORK/co-api.txt" "--artifacts-path artifacts/all-states/co"

echo "$dc_api" > "$WORK/dc-api.txt"
assert_contains "$WORK/dc-api.txt" "STATE=dc"
assert_contains "$WORK/dc-api.txt" "ASPNETCORE_URLS=http://localhost:5281"
assert_contains "$WORK/dc-api.txt" "Database=SebtPortal_dc"
assert_contains "$WORK/dc-api.txt" "--artifacts-path artifacts/all-states/dc"

echo "$co_web" > "$WORK/co-web.txt"
assert_contains "$WORK/co-web.txt" "STATE=co"
assert_contains "$WORK/co-web.txt" "BACKEND_URL=http://localhost:5280"
assert_contains "$WORK/co-web.txt" "NEXT_DIST_DIR=.next-co"
assert_contains "$WORK/co-web.txt" "--port 3000"

echo "$dc_web" > "$WORK/dc-web.txt"
assert_contains "$WORK/dc-web.txt" "STATE=dc"
assert_contains "$WORK/dc-web.txt" "BACKEND_URL=http://localhost:5281"
assert_contains "$WORK/dc-web.txt" "NEXT_DIST_DIR=.next-dc"
assert_contains "$WORK/dc-web.txt" "--port 3002"
echo "[run-all-states_test] case 1: OK"

# --- Test 2: the database connection follows .env, and the password is never printed ---
echo "[run-all-states_test] case 2: reads .env and masks the password"
assert_contains "$WORK/co-api.txt" "Server=localhost,14330"
assert_contains "$WORK/out.txt" "Password=***"
assert_not_contains "$WORK/out.txt" "Local-Only-Pa55word"
echo "[run-all-states_test] case 2: OK"

# --- Test 3: DC is served from its own hostname so its session cookie stays apart from CO's ---
echo "[run-all-states_test] case 3: announces per-state URLs"
assert_contains "$WORK/out.txt" "http://localhost:3000"
assert_contains "$WORK/out.txt" "http://dc.localhost:3002"
echo "[run-all-states_test] case 3: OK"

# --- Test 4: without .env, the compose defaults apply ---
echo "[run-all-states_test] case 4: falls back to compose defaults"
rm "$WORK/.env"
bash "$SCRIPT" --dry-run --root "$WORK" > "$WORK/out.txt"
process_line co:api > "$WORK/co-api.txt"
assert_contains "$WORK/co-api.txt" "Server=localhost,1433;"
assert_contains "$WORK/co-api.txt" "User Id=sa;"
echo "[run-all-states_test] case 4: OK"

# --- Test 5: --root without a directory shows usage instead of an unbound-variable crash ---
echo "[run-all-states_test] case 5: --root needs a value"
set +e
bash "$SCRIPT" --dry-run --root > "$WORK/out.txt" 2>&1
status=$?
set -e
assert_eq "$status" "2"
assert_contains "$WORK/out.txt" "Usage:"
assert_not_contains "$WORK/out.txt" "unbound variable"
echo "[run-all-states_test] case 5: OK"

echo "run-all-states_test: OK"
