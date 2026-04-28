#!/usr/bin/env bash
# Smoke test for scripts/ci/publish-api.sh.
# Asserts the resulting directory has the structure the bundle step expects.
#
# Pre-req: src/SEBT.Portal.Api/plugins-dc/ must already contain DC plugin DLLs
# (populated by building the DC connector, which has a CopyPlugins MSBuild target
# that runs AfterTargets="Build"). The smoke test does NOT build the DC connector
# itself — too heavy for a smoke test — so it skips when plugins-dc is empty.
set -e
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

# Skip locally if the API plugins-dc dir is empty (no DC connector built).
PLUGIN_DIR="$PROJECT_ROOT/src/SEBT.Portal.Api/plugins-dc"
if [ ! -d "$PLUGIN_DIR" ] || [ -z "$(ls -A "$PLUGIN_DIR" 2>/dev/null | grep -E '\.dll$' || true)" ]; then
  echo "SKIP: $PLUGIN_DIR has no plugin DLLs (build the DC connector first to populate it)"
  exit 0
fi

OUT_DIR="$(mktemp -d)"
trap 'rm -rf "$OUT_DIR"' EXIT

# Clean the source tree's build artifacts before running the test.
# This ensures we start fresh and can verify no new pollution is created.
find "$PROJECT_ROOT/src" -maxdepth 2 -type d \( -name "obj" -o -name "bin" \) -prune -exec rm -rf {} + 2>/dev/null || true

bash "$PROJECT_ROOT/scripts/ci/publish-api.sh" \
  --output "$OUT_DIR" \
  --build-state-dir "$OUT_DIR"

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
