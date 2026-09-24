#!/usr/bin/env bash
# Smoke tests for write-checker-config.sh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

SCRIPT="$PROJECT_ROOT/scripts/ci/write-checker-config.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
CONFIG="$WORK/config.js"

# Runs the script with only the given variables set, so nothing from the calling
# environment can leak into a case.
run() {
  env -i PATH="$PATH" "$@" bash "$SCRIPT" "$CONFIG"
}

# Evaluates the written config.js the way a browser would and prints one expression.
# eval is deliberate: the file under test is script output from this run, and
# running it is the only way to prove its values cannot escape into code.
read_config() {
  node -e '
    const window = {}
    eval(require("node:fs").readFileSync(process.argv[1], "utf8"))
    const config = window.__CHECKER_CONFIG__
    console.log(JSON.stringify(eval(process.argv[2])))
  ' "$CONFIG" "$1"
}

expect_failure() {
  local message="$1"
  shift
  rm -f "$CONFIG"
  if run "$@" 2>"$WORK/stderr"; then
    echo "ASSERT FAIL: expected the script to fail for: $*" >&2
    exit 1
  fi
  assert_contains "$WORK/stderr" "$message"
  if [ -f "$CONFIG" ]; then
    echo "ASSERT FAIL: config.js should not be written when validation fails" >&2
    exit 1
  fi
}

# --- Test 1: configured values are written with the right types ---
echo "[write-checker-config_test] case 1: writes configured values"
run API_BASE_URL=https://api.example.gov PORTAL_URL=https://portal.example.gov \
  AMPLITUDE_API_KEY=amp-key CHECKER_ENABLED=false SHOW_SCHOOL_FIELD=true
assert_file_exists "$CONFIG"
assert_eq "$(read_config 'config.apiBaseUrl')" '"https://api.example.gov"'
assert_eq "$(read_config 'config.amplitudeApiKey')" '"amp-key"'
assert_eq "$(read_config 'config.checkerEnabled')" 'false'
assert_eq "$(read_config 'config.showSchoolField')" 'true'
echo "[write-checker-config_test] case 1: OK"

# --- Test 2: a value with quotes and backslashes cannot break out of its string ---
echo "[write-checker-config_test] case 2: escapes values"
run META_PIXEL_ACTION='closing"quote and \backslash'
assert_eq "$(read_config 'config.metaPixelAction')" '"closing\"quote and \\backslash"'
echo "[write-checker-config_test] case 2: OK"

# --- Test 3: unset and blank values are left out, so build-time defaults apply ---
echo "[write-checker-config_test] case 3: omits blank values"
run PORTAL_URL='   ' AMPLITUDE_API_KEY='' CHECKER_ENABLED=''
assert_eq "$(read_config 'Object.keys(config).length')" '0'
echo "[write-checker-config_test] case 3: OK"

# --- Test 4: malformed values fail the step and write nothing ---
echo "[write-checker-config_test] case 4: rejects malformed values"
expect_failure "APPLICATION_URL must be an http(s) URL" APPLICATION_URL='not a url'
expect_failure "PORTAL_URL must be an http(s) URL" PORTAL_URL='javascript:alert(1)'
expect_failure 'BOT_PROTECTION_ENABLED must be "true" or "false"' BOT_PROTECTION_ENABLED=yes
echo "[write-checker-config_test] case 4: OK"

# --- Test 5: the output path is required ---
echo "[write-checker-config_test] case 5: requires an output path"
if env -i PATH="$PATH" bash "$SCRIPT" 2>/dev/null; then
  echo "ASSERT FAIL: expected a usage error with no arguments" >&2
  exit 1
fi
echo "[write-checker-config_test] case 5: OK"

echo "write-checker-config_test: OK"
