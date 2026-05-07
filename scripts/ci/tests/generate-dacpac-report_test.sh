#!/usr/bin/env bash
# Smoke tests for generate-dacpac-report.sh.
# Test 1: first-run path — pattern matches no prior release, emits initial-release CHANGELOG.
# Test 2 (skipped locally): delta path requires sqlpackage, gh, and a prior release.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- Test 1: first-run path ---
echo "[generate-dacpac-report_test] case 1: first-run"
# Create a dummy dacpac file (content does not matter since we won't run sqlpackage)
echo "fake dacpac content" > "$WORK/sebt-portal-test.dacpac"
mkdir -p "$WORK/out"

bash "$PROJECT_ROOT/scripts/ci/generate-dacpac-report.sh" \
  --dacpac "$WORK/sebt-portal-test.dacpac" \
  --output-dir "$WORK/out" \
  --prev-tag-pattern "release/dc-vNEVER-MATCHES-*"

assert_file_exists "$WORK/out/CHANGELOG-DACPAC.md"
assert_contains "$WORK/out/CHANGELOG-DACPAC.md" "Initial release"
assert_contains "$WORK/out/CHANGELOG-DACPAC.md" "no prior baseline"
assert_file_exists "$WORK/out/deploy-report.xml"
assert_file_exists "$WORK/out/deploy-report.html"
assert_contains "$WORK/out/deploy-report.xml" "InitialRelease"
assert_contains "$WORK/out/deploy-report.html" "Initial release"

echo "[generate-dacpac-report_test] case 1: OK"

# --- Test 2: delta path ---
# Skipped unless we have sqlpackage, gh, and a way to mock a prior release locally.
# In CI, the workflow exercises this end-to-end against the real GitHub Releases
# of the repo. No automated unit coverage at this level.
echo "[generate-dacpac-report_test] case 2: SKIP (covered by the workflow's first-real-release run)"

echo "generate-dacpac-report_test: OK"
