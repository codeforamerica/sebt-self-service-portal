#!/usr/bin/env bash
# Smoke test for scripts/ci/publish-api.sh.
# Asserts the resulting directory has the structure the bundle step expects.
#
# Pre-req: src/SEBT.Portal.Api/plugins-dc/ must already contain DC plugin DLLs.
# The smoke test does NOT build or publish the DC connector itself — too heavy
# for a smoke test — so it skips when plugins-dc is empty.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

# Skip locally if the API plugins-dc dir is empty (no plugin DLLs staged).
PLUGIN_DIR="$PROJECT_ROOT/src/SEBT.Portal.Api/plugins-dc"
if [ ! -d "$PLUGIN_DIR" ] || [ -z "$(ls -A "$PLUGIN_DIR" 2>/dev/null | grep -E '\.dll$' || true)" ]; then
  echo "SKIP: $PLUGIN_DIR has no plugin DLLs (stage the DC connector publish output first)"
  exit 0
fi

OUT_DIR="$(mktemp -d)"
trap 'rm -rf "$OUT_DIR"' EXIT

# This test runs a real `dotnet publish` against the live source tree. It will
# leave behind populated obj/ and bin/ directories under src/SEBT.Portal.Api/ —
# this is normal .NET behavior; those directories are .gitignored.
bash "$PROJECT_ROOT/scripts/ci/publish-api.sh" --output "$OUT_DIR"

assert_dir_exists "$OUT_DIR/api"
assert_file_exists "$OUT_DIR/api/SEBT.Portal.Api.dll"
assert_dir_exists "$OUT_DIR/api/plugins-dc"
# Plugin dir should have at least one DLL (copied by the API csproj's <None> rule)
test -n "$(ls "$OUT_DIR/api/plugins-dc"/*.dll 2>/dev/null)" || {
  echo "ASSERT FAIL: api/plugins-dc/ has no DLLs" >&2
  exit 1
}
assert_file_exists "$OUT_DIR/api/appsettings.prod.example.json"
assert_file_exists "$OUT_DIR/api/web.config"
assert_contains "$OUT_DIR/api/web.config" 'stdoutLogEnabled="true"'
assert_dir_exists "$OUT_DIR/api/logs"
assert_file_exists "$OUT_DIR/api/logs/.gitkeep"

echo "publish-api_test: OK"
