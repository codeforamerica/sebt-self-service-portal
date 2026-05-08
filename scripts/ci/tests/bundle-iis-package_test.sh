#!/usr/bin/env bash
# Smoke test for bundle-iis-package.sh: synthesize fake inputs, run the script,
# assert the resulting zip's structure.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

WORK="$(mktemp -d)"
EXTRACTED="$(mktemp -d)"
OUT_ZIP="output/sebt-dc-iis-1.0.0-test.zip"
ABSOLUTE_OUT_ZIP="$PROJECT_ROOT/$OUT_ZIP"
trap 'rm -rf "$WORK" "$EXTRACTED" && rm -f "$ABSOLUTE_OUT_ZIP"' EXIT

# Synthesize a fake api/ publish dir
mkdir -p "$WORK/api/plugins-dc"
echo "fake dll" > "$WORK/api/SEBT.Portal.Api.dll"
echo "fake plugin" > "$WORK/api/plugins-dc/Plugin.dll"
echo "{}" > "$WORK/api/appsettings.prod.example.json"
cat > "$WORK/api/web.config" <<'XML'
<?xml version="1.0"?><configuration><system.webServer><aspNetCore stdoutLogEnabled="true" /></system.webServer></configuration>
XML
mkdir -p "$WORK/api/logs"
touch "$WORK/api/logs/.gitkeep"

# Synthesize a fake web zip (directory tree as zip).
# bundle-iis-package.sh expects the zip's top-level dir to contain the web tree;
# package-frontend.sh's real output uses 'sebt-web/' as that root dir.
mkdir -p "$WORK/web-staging/src/SEBT.Portal.Web/.next" "$WORK/web-staging/src/SEBT.Portal.Web/public" "$WORK/web-staging/node_modules"
echo "console.log('hi')" > "$WORK/web-staging/src/SEBT.Portal.Web/server.js"
(cd "$WORK" && zip -rq web.zip web-staging)

# Synthesize a fake dacpac and report files
echo "fake dacpac" > "$WORK/sebt-portal-1.0.0.dacpac"
printf '# DACPAC schema changes\n\nInitial release — full schema.\n' > "$WORK/CHANGELOG-DACPAC.md"
echo "<DeployReport/>" > "$WORK/deploy-report.xml"
echo "<html/>" > "$WORK/deploy-report.html"

(
  cd "$PROJECT_ROOT"
  bash "$PROJECT_ROOT/scripts/ci/bundle-iis-package.sh" \
    --api-dir "$WORK/api" \
    --web-zip "$WORK/web.zip" \
    --dacpac "$WORK/sebt-portal-1.0.0.dacpac" \
    --changelog "$WORK/CHANGELOG-DACPAC.md" \
    --deploy-report-xml "$WORK/deploy-report.xml" \
    --deploy-report-html "$WORK/deploy-report.html" \
    --version "1.0.0" \
    --git-sha "deadbeef" \
    --output "$OUT_ZIP"
)

assert_file_exists "$ABSOLUTE_OUT_ZIP"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "README.md"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "CHANGELOG-DACPAC.md"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "api/SEBT.Portal.Api.dll"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "api/plugins-dc/Plugin.dll"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "api/appsettings.prod.example.json"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "api/web.config"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "web/web.config"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "web/src/SEBT.Portal.Web/server.js"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "dacpac/sebt-portal-1.0.0.dacpac"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "dacpac/deploy-report.xml"
assert_zip_contains "$ABSOLUTE_OUT_ZIP" "dacpac/deploy-report.html"

# Verify template substitution actually happened
unzip -q "$ABSOLUTE_OUT_ZIP" -d "$EXTRACTED"
README_PATH="$(ls "$EXTRACTED"/*/README.md 2>/dev/null || true)"
if [ -z "$README_PATH" ]; then
  echo "ASSERT FAIL: README.md not found at expected location inside zip" >&2
  find "$EXTRACTED" -maxdepth 3 -type f >&2
  exit 1
fi

assert_contains "$README_PATH" "Version:** 1.0.0"
assert_contains "$README_PATH" "deadbeef"
assert_contains "$README_PATH" "sebt-portal-1.0.0.dacpac"
# The {{...}} placeholders should all be resolved
if grep -F '{{' "$README_PATH"; then
  echo "ASSERT FAIL: README still contains unresolved {{...}} placeholders" >&2
  exit 1
fi

echo "bundle-iis-package_test: OK"
