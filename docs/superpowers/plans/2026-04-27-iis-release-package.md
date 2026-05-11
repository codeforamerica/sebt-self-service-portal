# IIS Release Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add CI scripts and a workflow that produce `sebt-dc-iis-{version}.zip` — a self-contained bundle containing the .NET API published for win-x64, the Next.js standalone web bundle, the EF-derived DACPAC, a schema-change report vs. the previous release, and a deployment README for the IIS server admin.

**Architecture:** New `release-iis-dc.yaml` GitHub Actions workflow triggers on `workflow_dispatch` and on `release/dc-v*` tag pushes. It chains six bash scripts under `scripts/ci/` (one new patch + three new scripts + two new template files) to produce the bundle. The DACPAC delta uses `sqlpackage /Action:DeployReport` against the previous release's dacpac downloaded via `gh release download`; first run emits an "initial release" message instead of failing. Each shell script ships with a sibling `*_test.sh` smoke test that runs in CI on PRs touching `scripts/ci/`.

**Tech Stack:** Bash (POSIX-compatible per existing convention), GitHub Actions on `ubuntu-latest`, `dotnet publish`, `sqlpackage`, `dotnet-ef`, `gh` CLI, `zip`.

**Reference spec:** [docs/superpowers/specs/2026-04-27-iis-release-package-design.md](../specs/2026-04-27-iis-release-package-design.md)

**Branch:** `chore/iis-release-package` (already created; spec doc lives there).

---

## File Structure

| File                                              | Status | Responsibility                                                                                                                                                                                                                                                           |
| ------------------------------------------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `scripts/ci/build-backend.sh`                     | Modify | Add `build_state_connector_package` function (currently missing vs. the canonical copy under `.github/workflows/scripts/`) so the new workflow can call this script and get the connector NuGet built.                                                                   |
| `scripts/ci/templates/web.config`                 | Create | Static HttpPlatformHandler config for the Next.js bundle on IIS. Copied verbatim into the bundle.                                                                                                                                                                        |
| `scripts/ci/templates/README.iis.md.tmpl`         | Create | Mustache-style template for the per-release deployment README. Variables: `{{VERSION}}`, `{{BUILD_DATE}}`, `{{GIT_SHA}}`, `{{DACPAC_FILENAME}}`, `{{DACPAC_SUMMARY}}`.                                                                                                   |
| `scripts/ci/publish-api.sh`                       | Create | `dotnet publish` API for win-x64; emit `appsettings.prod.example.json`; enable stdout logging in the API's auto-generated `web.config`. (DC plugin DLLs are copied into `plugins-dc/` by the DC connector's MSBuild `CopyPlugins` target before this runs — see Task 7.) |
| `scripts/ci/generate-dacpac-report.sh`            | Create | Use `gh` to find the previous `release/dc-v*` dacpac; run `sqlpackage /Action:DeployReport`; render XML→HTML; write `CHANGELOG-DACPAC.md`. First-run path when no prior release exists.                                                                                  |
| `scripts/ci/bundle-iis-package.sh`                | Create | Assemble the directory tree, render README from template, zip it to `sebt-dc-iis-{version}.zip`.                                                                                                                                                                         |
| `scripts/ci/tests/_assert.sh`                     | Create | Tiny shared assertion helpers (`assert_file_exists`, `assert_dir_exists`, `assert_contains`, `assert_eq`). Sourced by every `*_test.sh`.                                                                                                                                 |
| `scripts/ci/tests/publish-api_test.sh`            | Create | Smoke test for `publish-api.sh`.                                                                                                                                                                                                                                         |
| `scripts/ci/tests/generate-dacpac-report_test.sh` | Create | Smoke tests for both branches of `generate-dacpac-report.sh` (first-run, delta-found).                                                                                                                                                                                   |
| `scripts/ci/tests/bundle-iis-package_test.sh`     | Create | Smoke test for `bundle-iis-package.sh` with synthetic inputs.                                                                                                                                                                                                            |
| `scripts/ci/tests/run-all.sh`                     | Create | Runs every `*_test.sh` in this dir, exits non-zero on any failure.                                                                                                                                                                                                       |
| `.github/workflows/release-iis-dc.yaml`           | Create | The release workflow.                                                                                                                                                                                                                                                    |

---

## Task 1: Restore `build_state_connector_package` in `scripts/ci/build-backend.sh`

**Why:** The new workflow will call `scripts/ci/build-backend.sh` to produce the state-connector NuGet that the API plugin compilation needs. The canonical copy of this function lives at `.github/workflows/scripts/build-backend.sh:125-153`. Port it back to the `scripts/ci/` copy so the new workflow has a self-sufficient script to call. We are explicitly **not** consolidating the two script directories in this work.

**Files:**

- Modify: `scripts/ci/build-backend.sh`

- [ ] **Step 1: Read the canonical source to copy from**

Run: `sed -n '125,153p' .github/workflows/scripts/build-backend.sh`

Expected: a function `build_state_connector_package()` that builds and packs `state-connector/src/SEBT.Portal.StatesPlugins.Interfaces` to a per-environment NuGet output path (Docker / GitHub Actions / local).

- [ ] **Step 2: Add the function to `scripts/ci/build-backend.sh`**

Insert the function block immediately after the `build_backend()` function (after the closing brace on the line that currently reads `log_success "Backend build complete"` two lines later). Adjust the GitHub Actions branch's `PACKAGE_OUTPUT` value: in `scripts/ci/build-backend.sh`, `$PROJECT_ROOT` already points at the repo root, so `PACKAGE_OUTPUT="$PROJECT_ROOT/../nuget-store"` (one `..`, not three) — confirm by reading the path arithmetic at the top of the file.

```bash
# Build state connector package
build_state_connector_package() {
  log_info "Building state connector package..."
  cd "$PROJECT_ROOT/state-connector/src/SEBT.Portal.StatesPlugins.Interfaces"

  if [ -f /.dockerenv ]; then
    PACKAGE_OUTPUT="/root/nuget-store"
  elif [ -n "${DOCKER_HOST:-}" ]; then
    PACKAGE_OUTPUT="/root/nuget-store"
  elif [ -n "${GITHUB_ACTIONS:-}" ]; then
    PACKAGE_OUTPUT="$PROJECT_ROOT/../nuget-store"
  else
    PACKAGE_OUTPUT="./nuget-store"
  fi

  dotnet build SEBT.Portal.StatesPlugins.Interfaces.csproj \
    -p:GeneratePackageOnBuild=false \
    --configuration "$CONFIGURATION" \
    --verbosity minimal

  dotnet pack SEBT.Portal.StatesPlugins.Interfaces.csproj \
    --no-build \
    --configuration "$CONFIGURATION" \
    --output "$PACKAGE_OUTPUT" \
    --verbosity minimal

  log_success "State connector package built"
}
```

- [ ] **Step 3: Wire the function into `main()`**

In `scripts/ci/build-backend.sh`, locate the `main()` function and add a call to `build_state_connector_package` immediately after `build_backend`, guarded so we skip it when the state-connector dir isn't checked out (matters for ad-hoc local runs):

```bash
  if [ -d "$PROJECT_ROOT/state-connector" ]; then
    build_state_connector_package
  else
    log_info "Skipping state connector package (state-connector dir not present)"
  fi
```

- [ ] **Step 4: Verify the script still parses and runs locally**

Run: `bash -n scripts/ci/build-backend.sh && echo OK`
Expected: `OK` (just a parse check; do not run the full build here).

- [ ] **Step 5: Commit**

```bash
git add scripts/ci/build-backend.sh
git commit -m "Restore state-connector package step in scripts/ci/build-backend.sh"
```

---

## Task 2: Create the static templates directory

**Files:**

- Create: `scripts/ci/templates/web.config`
- Create: `scripts/ci/templates/README.iis.md.tmpl`

- [ ] **Step 1: Create the `web.config` for the Next.js tier**

This is the file the user already validated in their IDE selection. Drop it in verbatim.

```bash
mkdir -p scripts/ci/templates
```

Write `scripts/ci/templates/web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="httpPlatformHandler" path="*" verb="*" modules="httpPlatformHandler" />
    </handlers>
    <httpPlatform processPath="node.exe"
                  arguments="src\SEBT.Portal.Web\server.js"
                  startupTimeLimit="60"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\node-stdout">
      <environmentVariables>
        <environmentVariable name="PORT" value="%HTTP_PLATFORM_PORT%" />
        <environmentVariable name="HOSTNAME" value="127.0.0.1" />
        <environmentVariable name="NODE_ENV" value="production" />
        <environmentVariable name="BACKEND_URL" value="http://localhost:5280" />
        <environmentVariable name="NEXT_PUBLIC_STATE" value="dc" />
      </environmentVariables>
    </httpPlatform>
    <security>
      <requestFiltering>
        <hiddenSegments>
          <add segment="node_modules" />
          <add segment=".next" />
        </hiddenSegments>
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

- [ ] **Step 2: Create the README template**

Write `scripts/ci/templates/README.iis.md.tmpl`:

```markdown
# SEBT Portal — DC — IIS Deployment Package

**Version:** {{VERSION}}
**Built:** {{BUILD_DATE}} from commit {{GIT_SHA}}

This package is a self-contained drop-in for a Windows Server / IIS environment. It contains the .NET API, the Next.js web bundle, a DACPAC with the database schema, and a delta report describing how that schema differs from the previous release. The IIS administrator deploys the API and web tiers; the DACPAC is **not** applied by the IIS admin — it goes to the DBA via a ticket.

## What's in this package
```

.
├── README.md # this file
├── CHANGELOG-DACPAC.md # schema delta vs the previous release
├── api/
│ ├── _.exe, _.dll # ASP.NET Core API, framework-dependent win-x64
│ ├── plugins-dc/ # DC state connector plugin DLLs (loaded at startup)
│ ├── appsettings.json # base config (do not edit on the server)
│ ├── appsettings.prod.example.json # template for DC-specific / secret values
│ ├── web.config # ASP.NET Core Module config (stdout logging enabled)
│ └── logs/ # API stdout log directory
├── web/
│ ├── web.config # HttpPlatformHandler config for Node.js
│ ├── src/SEBT.Portal.Web/server.js # the Next.js standalone server entrypoint
│ ├── .next/, public/, node_modules/ # already hoisted; no symlinks
│ └── logs/ # Node.js stdout log directory
└── dacpac/
├── {{DACPAC_FILENAME}}
├── deploy-report.xml # raw sqlpackage DeployReport
└── deploy-report.html # human-readable version

```

## Schema changes since the previous release

{{DACPAC_SUMMARY}}

The full machine-readable report is at `dacpac/deploy-report.xml`. The human-readable rendering is at `dacpac/deploy-report.html`. `CHANGELOG-DACPAC.md` has the full grouped summary.

## Prerequisites on the IIS server

Install once per server. None of these are bundled in this zip.

- **Windows Server** 2019+ with IIS enabled.
- **ASP.NET Core 10 Hosting Bundle.** Download from <https://dotnet.microsoft.com/download/dotnet/10.0>. After install, run `iisreset` once.
- **Node.js 24 LTS (x64)** — the MSI installer from <https://nodejs.org>. Confirm with `node -v`.
- **HttpPlatformHandler module for IIS** — install via Web Platform Installer or download from Microsoft. Required for the web tier.
- **URL Rewrite module for IIS** — install from <https://www.iis.net/downloads/microsoft/url-rewrite>.

## Deployment steps

1. **Extract** this zip to a deployment root, e.g. `C:\inetpub\sebt-dc\{{VERSION}}`.
2. **Create the IIS site** for the web tier pointing at `<root>\web`. Set Application Pool to **No Managed Code** and **No** for "Load User Profile."
3. **Create an application** under that site at the path `/api`, pointing at `<root>\api`. Set Application Pool to **.NET CLR v4.0** with managed pipeline mode **Integrated**.
4. **Configure the API.** Copy `api\appsettings.prod.example.json` to `api\appsettings.Production.json` and fill in the DC-specific / secret values (connection string from your DBA, Socure / IdP secrets, etc.). The base `appsettings.json` already contains all defaults; you only need to set the keys present in the example file.
5. **Set ACLs** on `api\logs\` and `web\logs\` to grant the app pool identity (typically `IIS APPPOOL\<your pool name>`) **Modify** permission.
6. **Verify environment variables** in `web\web.config`. The shipped values point `BACKEND_URL` at `http://localhost:5280` — adjust if your API binding port differs. `NEXT_PUBLIC_STATE` is `dc`; do not change.
7. **Start the site.** Hit `https://<host>/` for the web app and `https://<host>/api/health` (or your configured health endpoint) for the API.

## Database update — DBA ticket

**The IIS administrator does not run the DACPAC.** Open a DBA ticket using the template below.

```

Subject: SEBT Portal DC v{{VERSION}} — DACPAC application

The IIS deployment of SEBT Portal DC v{{VERSION}} is in place and requires a database schema update before it can be activated.

Package version: {{VERSION}}
Built: {{BUILD_DATE}} from commit {{GIT_SHA}}
DACPAC filename: {{DACPAC_FILENAME}}
Schema delta: see attached CHANGELOG-DACPAC.md and deploy-report.html

Apply with sqlpackage:

sqlpackage /Action:Publish ^
/SourceFile:{{DACPAC_FILENAME}} ^
/TargetConnectionString:"<production connection string>"

For a dry run / pre-flight first:

sqlpackage /Action:DeployReport ^
/SourceFile:{{DACPAC_FILENAME}} ^
/TargetConnectionString:"<production connection string>" ^
/OutputPath:dba-pre-flight-report.xml

Please apply during the agreed maintenance window and confirm completion.

```

## Verification

- API health: `https://<host>/api/health`
- Web home: `https://<host>/`
- API logs: `api\logs\stdout_*.log`
- Web logs: `web\logs\node-stdout*.log`

## Rollback

Keep the previous deployment directory in place (e.g. `C:\inetpub\sebt-dc\<previous-version>`). To roll back the application tier, point the IIS site's physical path back to that directory and restart the app pool. **Schema rollback is a separate DBA ticket** — DACPACs do not auto-rollback; the DBA decides whether to revert based on the delta in this release's `CHANGELOG-DACPAC.md`.
```

- [ ] **Step 3: Commit**

```bash
git add scripts/ci/templates/web.config scripts/ci/templates/README.iis.md.tmpl
git commit -m "Add IIS web.config and README template for release bundle"
```

---

## Task 3: Add the test helpers and runner

**Files:**

- Create: `scripts/ci/tests/_assert.sh`
- Create: `scripts/ci/tests/run-all.sh`

- [ ] **Step 1: Write the assertion helpers**

Create `scripts/ci/tests/_assert.sh`:

```bash
#!/usr/bin/env bash
# Tiny assertion helpers shared by scripts/ci/tests/*_test.sh.
# Source this file at the top of each test:  source "$(dirname "$0")/_assert.sh"
# Each assertion prints to stderr and exits non-zero on failure (the parent test fails fast).

set -e
set -u

assert_file_exists() {
  local path="$1"
  if [ ! -f "$path" ]; then
    echo "ASSERT FAIL: expected file to exist: $path" >&2
    exit 1
  fi
}

assert_dir_exists() {
  local path="$1"
  if [ ! -d "$path" ]; then
    echo "ASSERT FAIL: expected dir to exist: $path" >&2
    exit 1
  fi
}

assert_contains() {
  local file="$1"
  local needle="$2"
  if ! grep -qF -- "$needle" "$file"; then
    echo "ASSERT FAIL: expected file '$file' to contain: $needle" >&2
    exit 1
  fi
}

assert_eq() {
  local actual="$1"
  local expected="$2"
  if [ "$actual" != "$expected" ]; then
    echo "ASSERT FAIL: expected '$expected', got '$actual'" >&2
    exit 1
  fi
}

assert_zip_contains() {
  local zip_path="$1"
  local entry="$2"
  if ! unzip -l "$zip_path" | grep -qF -- "$entry"; then
    echo "ASSERT FAIL: expected zip '$zip_path' to contain entry: $entry" >&2
    unzip -l "$zip_path" >&2
    exit 1
  fi
}
```

- [ ] **Step 2: Write the test runner**

Create `scripts/ci/tests/run-all.sh`:

```bash
#!/usr/bin/env bash
# Runs every *_test.sh in this directory. Exits non-zero on the first failure.
set -e
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
shopt -s nullglob

failed=0
for test in "$SCRIPT_DIR"/*_test.sh; do
  echo "=== Running: $(basename "$test") ==="
  if bash "$test"; then
    echo "PASS: $(basename "$test")"
  else
    echo "FAIL: $(basename "$test")"
    failed=$((failed + 1))
  fi
  echo ""
done

if [ "$failed" -gt 0 ]; then
  echo "$failed test(s) failed."
  exit 1
fi

echo "All tests passed."
```

- [ ] **Step 3: Make the scripts executable**

Run: `chmod +x scripts/ci/tests/_assert.sh scripts/ci/tests/run-all.sh`

- [ ] **Step 4: Verify the runner works with no tests yet**

Run: `bash scripts/ci/tests/run-all.sh`
Expected: `All tests passed.` (since `nullglob` makes the loop a no-op when no `*_test.sh` exist).

- [ ] **Step 5: Commit**

```bash
git add scripts/ci/tests/_assert.sh scripts/ci/tests/run-all.sh
git commit -m "Add shell-test assertion helpers and runner for scripts/ci/"
```

---

## Task 4: Build `scripts/ci/publish-api.sh`

**Why:** The release workflow needs an API publish step that produces a Windows / IIS-ready directory: framework-dependent `dotnet publish` for `win-x64`, an `appsettings.prod.example.json` listing only the DC-specific / secret keys, the auto-generated `web.config` patched to enable stdout logging, and a `logs/` directory ready for the app pool identity.

**Plan correction (vs the original draft):** The DC plugin DLLs are NOT manually copied here. They land in `src/SEBT.Portal.Api/plugins-dc/` as a side effect of building the DC connector — its csproj has a `CopyPlugins` MSBuild target that runs `AfterTargets="Build"` and copies its DLLs to the API's `plugins-dc/` directory. The API's `<None Include="plugins-dc\**\*.dll">` ItemGroup then sweeps them into the publish output automatically. Task 7 (workflow) is responsible for ordering: build DC connector BEFORE running this script. This script just runs `dotnet publish`, writes the appsettings example, and patches the web.config.

**Files:**

- Create: `scripts/ci/tests/publish-api_test.sh`
- Create: `scripts/ci/publish-api.sh`

- [ ] **Step 1: Write the failing smoke test**

Create `scripts/ci/tests/publish-api_test.sh`:

```bash
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
```

Make executable:

```bash
chmod +x scripts/ci/tests/publish-api_test.sh
```

- [ ] **Step 2: Run the test, confirm it fails**

Run: `bash scripts/ci/tests/publish-api_test.sh`
Expected: fails with `bash: scripts/ci/publish-api.sh: No such file or directory` (or skips if plugins-dc/ is empty — in CI the connector will be built first, so the assertions will run there).

- [ ] **Step 3: Implement `scripts/ci/publish-api.sh`**

Create `scripts/ci/publish-api.sh`:

```bash
#!/bin/bash
# API Publish Script
# Publishes the .NET API for win-x64 (framework-dependent) into <output>/api/,
# emits a secrets-only appsettings.prod.example.json, and patches the
# auto-generated web.config to enable ASP.NET Core stdout logging.
#
# Plugin DLLs are NOT copied by this script — they must already be in
# src/SEBT.Portal.Api/plugins-dc/ before this runs (populated by building the
# DC connector, whose MSBuild CopyPlugins target handles that). The API csproj's
# <None Include="plugins-dc\**\*.dll"> ItemGroup then picks them up during publish.
#
# Usage:
#   ./scripts/ci/publish-api.sh --output <dir> [--configuration Release]

set -e
set -u

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CONFIGURATION="Release"
OUTPUT_DIR=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --output) OUTPUT_DIR="$2"; shift 2 ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$OUTPUT_DIR" ]; then
  log_error "--output is required"
  exit 1
fi

# Sanity-check that the DC connector has already populated plugins-dc/.
PLUGIN_DIR="$PROJECT_ROOT/src/SEBT.Portal.Api/plugins-dc"
if [ -z "$(ls -A "$PLUGIN_DIR" 2>/dev/null | grep -E '\.dll$' || true)" ]; then
  log_warning "$PLUGIN_DIR has no DLLs — DC connector was not built before publish-api.sh."
  log_warning "The published output will not contain DC plugin DLLs."
fi

API_OUT="$OUTPUT_DIR/api"
mkdir -p "$API_OUT"

log_info "Publishing API to $API_OUT (configuration: $CONFIGURATION, runtime: win-x64)"
dotnet publish "$PROJECT_ROOT/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj" \
  --configuration "$CONFIGURATION" \
  --runtime win-x64 \
  --self-contained false \
  --output "$API_OUT" \
  -p:BuildFrontend=false \
  --verbosity minimal

log_info "Writing appsettings.prod.example.json (DC-specific / secret keys only)"
cat > "$API_OUT/appsettings.prod.example.json" <<'JSON'
{
  "_comment": "Copy this file to appsettings.Production.json and fill in DC production values. Defaults for everything not listed here come from appsettings.json. Do not commit your filled-in copy.",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_DB_HOST,1433;Database=SEBT_Portal_DC;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
  },
  "DCConnector": {
    "ConnectionString": "Server=YOUR_DC_SOURCE_DB_HOST,1433;Database=DcSource;User Id=YOUR_DC_SOURCE_USER;Password=YOUR_DC_SOURCE_PASSWORD;TrustServerCertificate=True;"
  },
  "Smarty": {
    "Enabled": true,
    "AuthId": "YOUR_SMARTY_AUTH_ID",
    "AuthToken": "YOUR_SMARTY_AUTH_TOKEN"
  },
  "Socure": {
    "ApiKey": "YOUR_SOCURE_PROD_API_KEY",
    "WebhookSecret": "YOUR_SOCURE_WEBHOOK_BEARER_TOKEN",
    "BaseUrl": "https://riskos.socure.com",
    "DiSessionToken": "YOUR_DI_SESSION_TOKEN"
  }
}
JSON

log_info "Patching web.config to enable stdout logging"
WEBCONFIG="$API_OUT/web.config"
if [ ! -f "$WEBCONFIG" ]; then
  log_error "Expected dotnet publish to emit web.config at: $WEBCONFIG"
  exit 1
fi
# The auto-generated config has stdoutLogEnabled="false" — flip it and set the path.
# Use a portable sed (works on macOS BSD sed and GNU sed because we provide '' as arg to -i for both branches).
if sed --version >/dev/null 2>&1; then
  # GNU sed
  sed -i 's/stdoutLogEnabled="false"/stdoutLogEnabled="true"/g' "$WEBCONFIG"
  sed -i 's|stdoutLogFile=".\\logs\\stdout"|stdoutLogFile=".\\logs\\stdout"|g' "$WEBCONFIG"
else
  # BSD sed (macOS)
  sed -i '' 's/stdoutLogEnabled="false"/stdoutLogEnabled="true"/g' "$WEBCONFIG"
fi

mkdir -p "$API_OUT/logs"
touch "$API_OUT/logs/.gitkeep"

log_success "API publish complete: $API_OUT"
```

Make executable:

```bash
chmod +x scripts/ci/publish-api.sh
```

- [ ] **Step 4: Run the smoke test, confirm it passes**

Pre-req for local run: build the DC connector once with the sibling-repo layout so `src/SEBT.Portal.Api/plugins-dc/` is populated. Run from `../sebt-self-service-portal-dc-connector`:

```bash
dotnet build src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj --configuration Release
```

The DC connector's `CopyPlugins` MSBuild target (defined in its csproj, runs `AfterTargets="Build"`) copies its DLLs into the portal repo's `src/SEBT.Portal.Api/plugins-dc/` automatically when the two repos are siblings.

Run: `bash scripts/ci/tests/publish-api_test.sh`
Expected: `publish-api_test: OK` (or `SKIP: ... no plugin DLLs` if you didn't build the DC connector yet — in CI the workflow builds the DC connector before invoking this script, so the assertions will run there).

- [ ] **Step 5: Commit**

```bash
git add scripts/ci/publish-api.sh scripts/ci/tests/publish-api_test.sh
git commit -m "Add publish-api.sh: dotnet publish for IIS with DC plugins and stdout logging"
```

---

## Task 5: Build `scripts/ci/generate-dacpac-report.sh`

**Why:** Produces `CHANGELOG-DACPAC.md` and `deploy-report.{xml,html}` so the DBA ticket has a clear summary of what schema changes are coming. Two distinct paths: when a previous `release/dc-v*` GitHub Release exists, run `sqlpackage /Action:DeployReport` against its dacpac; when no prior release exists (first release ever), emit an "initial release — full schema" CHANGELOG without invoking sqlpackage.

**Files:**

- Create: `scripts/ci/tests/generate-dacpac-report_test.sh`
- Create: `scripts/ci/generate-dacpac-report.sh`

- [ ] **Step 1: Write the failing test for the first-run path**

Create `scripts/ci/tests/generate-dacpac-report_test.sh`:

```bash
#!/usr/bin/env bash
# Smoke tests for generate-dacpac-report.sh.
# Test 1: first-run path — pattern matches no prior release, emits initial-release CHANGELOG.
# Test 2 (skipped locally): delta path requires sqlpackage, gh, and a prior release.
set -e
set -u

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

echo "[generate-dacpac-report_test] case 1: OK"

# --- Test 2: delta path ---
# Skipped unless we have sqlpackage, gh, and a way to mock a prior release locally.
# In CI, the workflow exercises this end-to-end against the real GitHub Releases
# of the repo. No automated unit coverage at this level.
echo "[generate-dacpac-report_test] case 2: SKIP (covered by the workflow's first-real-release run)"

echo "generate-dacpac-report_test: OK"
```

Make executable: `chmod +x scripts/ci/tests/generate-dacpac-report_test.sh`

- [ ] **Step 2: Run the test, confirm it fails**

Run: `bash scripts/ci/tests/generate-dacpac-report_test.sh`
Expected: fails because `scripts/ci/generate-dacpac-report.sh` doesn't exist yet.

- [ ] **Step 3: Implement `scripts/ci/generate-dacpac-report.sh`**

Create `scripts/ci/generate-dacpac-report.sh`:

```bash
#!/bin/bash
# DACPAC Delta Report Generator
# Compares the freshly built DACPAC against the previous release's DACPAC
# (downloaded from GitHub Releases) and emits a human-readable schema delta
# plus the raw sqlpackage DeployReport output.
#
# Usage:
#   ./scripts/ci/generate-dacpac-report.sh \
#       --dacpac <path> \
#       --output-dir <dir> \
#       [--prev-tag-pattern <glob>]   (default: release/dc-v*)
#       [--repo <owner/repo>]         (default: from gh repo view)
#
# Outputs (under <output-dir>):
#   CHANGELOG-DACPAC.md   summary
#   deploy-report.xml     raw DeployReport (only when prior release exists)
#   deploy-report.html    rendered version (only when prior release exists)
#
# First-run case: when no tag matching --prev-tag-pattern exists in the repo,
# CHANGELOG-DACPAC.md is written with an "Initial release" message and no
# DeployReport files are produced. The script exits 0.

set -e
set -u

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

DACPAC=""
OUT_DIR=""
PREV_TAG_PATTERN="release/dc-v*"
REPO=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --dacpac) DACPAC="$2"; shift 2 ;;
    --output-dir) OUT_DIR="$2"; shift 2 ;;
    --prev-tag-pattern) PREV_TAG_PATTERN="$2"; shift 2 ;;
    --repo) REPO="$2"; shift 2 ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$DACPAC" ] || [ -z "$OUT_DIR" ]; then
  log_error "--dacpac and --output-dir are required"
  exit 1
fi
if [ ! -f "$DACPAC" ]; then
  log_error "DACPAC not found: $DACPAC"
  exit 1
fi

mkdir -p "$OUT_DIR"

write_initial_release_changelog() {
  local reason="$1"
  log_info "Writing initial-release CHANGELOG (reason: $reason)"
  cat > "$OUT_DIR/CHANGELOG-DACPAC.md" <<EOF
# DACPAC schema changes

**Initial release — full schema (no prior baseline).**

This is the first packaged release for DC, or no prior \`$PREV_TAG_PATTERN\` release was found in the repo. There is no diff to report; the attached DACPAC contains the complete schema.

The DBA should treat this as a fresh deploy: \`sqlpackage /Action:Publish\` will create every object the schema defines.

EOF
}

# Resolve previous release tag via gh.
log_info "Looking for previous release matching: $PREV_TAG_PATTERN"
if ! command -v gh >/dev/null 2>&1; then
  log_warning "gh CLI not found — treating as first run"
  write_initial_release_changelog "gh CLI unavailable"
  log_success "Done."
  exit 0
fi

GH_REPO_FLAG=()
if [ -n "$REPO" ]; then
  GH_REPO_FLAG=(--repo "$REPO")
fi

# `gh release list` does not support globs natively, so we filter after.
PREV_TAG="$(gh release list "${GH_REPO_FLAG[@]}" --limit 100 --json tagName --jq '.[].tagName' \
  | grep -E "^${PREV_TAG_PATTERN//\*/.*}$" \
  | head -n 1 || true)"

if [ -z "$PREV_TAG" ]; then
  write_initial_release_changelog "no matching prior release"
  log_success "Done."
  exit 0
fi

log_info "Previous release: $PREV_TAG"

# Download the previous DACPAC asset.
PREV_DIR="$(mktemp -d)"
trap 'rm -rf "$PREV_DIR"' EXIT
log_info "Downloading previous DACPAC from $PREV_TAG"
gh release download "$PREV_TAG" "${GH_REPO_FLAG[@]}" --dir "$PREV_DIR" --pattern "*.dacpac" || {
  log_warning "No DACPAC asset on $PREV_TAG — treating as first run"
  write_initial_release_changelog "previous release had no DACPAC asset"
  log_success "Done."
  exit 0
}
PREV_DACPAC="$(ls "$PREV_DIR"/*.dacpac | head -n 1)"
log_info "Previous DACPAC: $PREV_DACPAC"

# Run sqlpackage DeployReport: source = new dacpac, target = previous dacpac.
# Direction note: this reports what the DBA must do to bring a database matching
# the *previous* schema up to the *new* schema. That's the migration the DBA runs.
if ! command -v sqlpackage >/dev/null 2>&1; then
  log_error "sqlpackage not found on PATH"
  exit 1
fi

log_info "Running sqlpackage DeployReport"
sqlpackage \
  /Action:DeployReport \
  /SourceFile:"$DACPAC" \
  /TargetFile:"$PREV_DACPAC" \
  /OutputPath:"$OUT_DIR/deploy-report.xml"

# Render to HTML using a tiny embedded XSLT-free transform: just wrap the raw
# XML in <pre>. sqlpackage's report XML is structured, but we don't need a
# pretty rendering for v1 — the XML and the markdown summary together cover
# the use case. (If we want a richer HTML in a later iteration, swap this.)
cat > "$OUT_DIR/deploy-report.html" <<HTML
<!doctype html>
<html><head><meta charset="utf-8"><title>DeployReport — vs $PREV_TAG</title>
<style>body{font-family:system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1rem}pre{background:#f5f5f5;padding:1rem;overflow:auto;font-size:.85rem}</style>
</head><body>
<h1>DACPAC DeployReport</h1>
<p><strong>New release:</strong> $(basename "$DACPAC")<br>
<strong>Compared against:</strong> $PREV_TAG ($(basename "$PREV_DACPAC"))</p>
<pre>
$(cat "$OUT_DIR/deploy-report.xml" | sed 's/&/\&amp;/g; s/</\&lt;/g; s/>/\&gt;/g')
</pre>
</body></html>
HTML

# Build a markdown summary by extracting Operation elements from the XML.
log_info "Building CHANGELOG-DACPAC.md"
{
  echo "# DACPAC schema changes"
  echo ""
  echo "**Compared against:** \`$PREV_TAG\`"
  echo ""
  if grep -q '<Operations>' "$OUT_DIR/deploy-report.xml"; then
    echo "## Summary"
    echo ""
    # Extract <Operation Name="..."> elements and the object names beneath each
    # via grep — XSLT would be nicer but adds a dependency. The structure is
    # documented at https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-report-options
    awk '
      /<Operation Name="/ {
        match($0, /Name="[^"]+"/);
        op = substr($0, RSTART+6, RLENGTH-7);
        print "### " op
        print ""
      }
      /<Item Type="/ {
        match($0, /Value="[^"]+"/);
        if (RLENGTH > 0) print "- " substr($0, RSTART+7, RLENGTH-8);
      }
      /<\/Operation>/ { print "" }
    ' "$OUT_DIR/deploy-report.xml"
  else
    echo "_No schema operations reported. The new DACPAC matches the previous release._"
    echo ""
  fi
  echo "## Files"
  echo ""
  echo "- Raw report: \`dacpac/deploy-report.xml\`"
  echo "- Rendered: \`dacpac/deploy-report.html\`"
} > "$OUT_DIR/CHANGELOG-DACPAC.md"

log_success "Done. Reports in $OUT_DIR"
```

Make executable: `chmod +x scripts/ci/generate-dacpac-report.sh`

- [ ] **Step 4: Run the test, confirm the first-run path passes**

Run: `bash scripts/ci/tests/generate-dacpac-report_test.sh`
Expected: `generate-dacpac-report_test: OK`. The test will print `SKIP` for the delta path since we don't mock GitHub Releases locally.

- [ ] **Step 5: Commit**

```bash
git add scripts/ci/generate-dacpac-report.sh scripts/ci/tests/generate-dacpac-report_test.sh
git commit -m "Add generate-dacpac-report.sh: schema delta vs previous release"
```

---

## Task 6: Build `scripts/ci/bundle-iis-package.sh`

**Why:** Final assembly step — takes the API publish dir, the web zip, the dacpac, and the report files; renders the README from `templates/README.iis.md.tmpl` with version/SHA/summary substitutions; produces the deliverable zip.

**Files:**

- Create: `scripts/ci/tests/bundle-iis-package_test.sh`
- Create: `scripts/ci/bundle-iis-package.sh`

- [ ] **Step 1: Write the failing smoke test**

Create `scripts/ci/tests/bundle-iis-package_test.sh`:

```bash
#!/usr/bin/env bash
# Smoke test for bundle-iis-package.sh: synthesize fake inputs, run the script,
# assert the resulting zip's structure.
set -e
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

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

# Synthesize a fake web zip (directory tree as zip)
mkdir -p "$WORK/web-staging/src/SEBT.Portal.Web/.next" "$WORK/web-staging/src/SEBT.Portal.Web/public" "$WORK/web-staging/node_modules"
echo "console.log('hi')" > "$WORK/web-staging/src/SEBT.Portal.Web/server.js"
(cd "$WORK" && zip -rq web.zip web-staging)

# Synthesize a fake dacpac and report files
echo "fake dacpac" > "$WORK/sebt-portal-1.0.0.dacpac"
echo "# DACPAC schema changes\n\nInitial release — full schema." > "$WORK/CHANGELOG-DACPAC.md"
echo "<DeployReport/>" > "$WORK/deploy-report.xml"
echo "<html/>" > "$WORK/deploy-report.html"

OUT_ZIP="$WORK/sebt-dc-iis-1.0.0.zip"

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

assert_file_exists "$OUT_ZIP"
assert_zip_contains "$OUT_ZIP" "README.md"
assert_zip_contains "$OUT_ZIP" "CHANGELOG-DACPAC.md"
assert_zip_contains "$OUT_ZIP" "api/SEBT.Portal.Api.dll"
assert_zip_contains "$OUT_ZIP" "api/plugins-dc/Plugin.dll"
assert_zip_contains "$OUT_ZIP" "api/appsettings.prod.example.json"
assert_zip_contains "$OUT_ZIP" "api/web.config"
assert_zip_contains "$OUT_ZIP" "web/web.config"
assert_zip_contains "$OUT_ZIP" "web/src/SEBT.Portal.Web/server.js"
assert_zip_contains "$OUT_ZIP" "dacpac/sebt-portal-1.0.0.dacpac"
assert_zip_contains "$OUT_ZIP" "dacpac/deploy-report.xml"
assert_zip_contains "$OUT_ZIP" "dacpac/deploy-report.html"

# Verify template substitution actually happened
EXTRACTED="$(mktemp -d)"
trap 'rm -rf "$WORK" "$EXTRACTED"' EXIT
unzip -q "$OUT_ZIP" -d "$EXTRACTED"
assert_contains "$EXTRACTED/README.md" "Version:** 1.0.0"
assert_contains "$EXTRACTED/README.md" "deadbeef"
assert_contains "$EXTRACTED/README.md" "sebt-portal-1.0.0.dacpac"
# The {{...}} placeholders should all be resolved
if grep -F '{{' "$EXTRACTED/README.md"; then
  echo "ASSERT FAIL: README still contains unresolved {{...}} placeholders" >&2
  exit 1
fi

echo "bundle-iis-package_test: OK"
```

Make executable: `chmod +x scripts/ci/tests/bundle-iis-package_test.sh`

- [ ] **Step 2: Run the test, confirm it fails**

Run: `bash scripts/ci/tests/bundle-iis-package_test.sh`
Expected: fails because `scripts/ci/bundle-iis-package.sh` doesn't exist yet.

- [ ] **Step 3: Implement `scripts/ci/bundle-iis-package.sh`**

Create `scripts/ci/bundle-iis-package.sh`:

```bash
#!/bin/bash
# IIS Bundle Assembler
# Combines API publish output, web zip, DACPAC, and reports into a single
# deployable zip with a generated README.
#
# Usage:
#   ./scripts/ci/bundle-iis-package.sh \
#       --api-dir <dir> \
#       --web-zip <path> \
#       --dacpac <path> \
#       --changelog <path>          # CHANGELOG-DACPAC.md
#       --deploy-report-xml <path>
#       --deploy-report-html <path>
#       --version <ver> \
#       --git-sha <sha> \
#       --output <zip>

set -e
set -u

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMPLATES_DIR="$SCRIPT_DIR/templates"

API_DIR=""; WEB_ZIP=""; DACPAC=""; CHANGELOG=""; REPORT_XML=""; REPORT_HTML=""
VERSION=""; GIT_SHA=""; OUT_ZIP=""

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --api-dir) API_DIR="$2"; shift 2 ;;
    --web-zip) WEB_ZIP="$2"; shift 2 ;;
    --dacpac) DACPAC="$2"; shift 2 ;;
    --changelog) CHANGELOG="$2"; shift 2 ;;
    --deploy-report-xml) REPORT_XML="$2"; shift 2 ;;
    --deploy-report-html) REPORT_HTML="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --git-sha) GIT_SHA="$2"; shift 2 ;;
    --output) OUT_ZIP="$2"; shift 2 ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

for var in API_DIR WEB_ZIP DACPAC CHANGELOG REPORT_XML REPORT_HTML VERSION GIT_SHA OUT_ZIP; do
  if [ -z "${!var}" ]; then
    log_error "$var is required"
    exit 1
  fi
done

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT
BUNDLE="$STAGING/sebt-dc-iis-$VERSION"
mkdir -p "$BUNDLE"

log_info "Staging API"
cp -R "$API_DIR" "$BUNDLE/api"

log_info "Staging web (extract zip and rename to web/)"
mkdir -p "$STAGING/web-extract"
unzip -q "$WEB_ZIP" -d "$STAGING/web-extract"
# The package-frontend.sh zip's root dir is `sebt-web/`. Move its contents into web/.
WEB_ROOT="$(ls -1d "$STAGING/web-extract"/*/ 2>/dev/null | head -n 1)"
if [ -z "$WEB_ROOT" ]; then
  log_error "Could not identify root dir inside web zip: $WEB_ZIP"
  exit 1
fi
mv "$WEB_ROOT" "$BUNDLE/web"
cp "$TEMPLATES_DIR/web.config" "$BUNDLE/web/web.config"
mkdir -p "$BUNDLE/web/logs"
touch "$BUNDLE/web/logs/.gitkeep"

log_info "Staging DACPAC and reports"
mkdir -p "$BUNDLE/dacpac"
cp "$DACPAC" "$BUNDLE/dacpac/"
cp "$REPORT_XML" "$BUNDLE/dacpac/deploy-report.xml"
cp "$REPORT_HTML" "$BUNDLE/dacpac/deploy-report.html"
cp "$CHANGELOG" "$BUNDLE/CHANGELOG-DACPAC.md"

log_info "Rendering README"
DACPAC_FILENAME="$(basename "$DACPAC")"
BUILD_DATE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
# Take the first ~10 non-empty lines after the title as the inline summary
DACPAC_SUMMARY="$(awk 'NR>1 && NF {print; n++; if (n>=10) exit}' "$CHANGELOG")"

# Use python for safe placeholder substitution. Pass values via env vars so
# multi-line markdown summaries with arbitrary content (including quotes) are
# handled correctly.
export TPL_VERSION="$VERSION"
export TPL_BUILD_DATE="$BUILD_DATE"
export TPL_GIT_SHA="$GIT_SHA"
export TPL_DACPAC_FILENAME="$DACPAC_FILENAME"
export TPL_DACPAC_SUMMARY="$DACPAC_SUMMARY"
python3 - "$TEMPLATES_DIR/README.iis.md.tmpl" "$BUNDLE/README.md" <<'PY'
import os, sys, pathlib
src, dst = sys.argv[1], sys.argv[2]
text = pathlib.Path(src).read_text()
subs = {
    "{{VERSION}}":         os.environ["TPL_VERSION"],
    "{{BUILD_DATE}}":      os.environ["TPL_BUILD_DATE"],
    "{{GIT_SHA}}":         os.environ["TPL_GIT_SHA"],
    "{{DACPAC_FILENAME}}": os.environ["TPL_DACPAC_FILENAME"],
    "{{DACPAC_SUMMARY}}":  os.environ["TPL_DACPAC_SUMMARY"],
}
for k, v in subs.items():
    text = text.replace(k, v)
pathlib.Path(dst).write_text(text)
PY

log_info "Zipping bundle to $OUT_ZIP"
mkdir -p "$(dirname "$OUT_ZIP")"
rm -f "$OUT_ZIP"
(cd "$STAGING" && zip -rqX "$OUT_ZIP" "sebt-dc-iis-$VERSION")

log_success "Bundle ready: $OUT_ZIP ($(du -h "$OUT_ZIP" | cut -f1))"
```

Make executable: `chmod +x scripts/ci/bundle-iis-package.sh`

- [ ] **Step 4: Run the test, confirm it passes**

Run: `bash scripts/ci/tests/bundle-iis-package_test.sh`
Expected: `bundle-iis-package_test: OK`

- [ ] **Step 5: Commit**

```bash
git add scripts/ci/bundle-iis-package.sh scripts/ci/tests/bundle-iis-package_test.sh
git commit -m "Add bundle-iis-package.sh: assemble final IIS deployment zip"
```

---

## Task 7: Add `release-iis-dc.yaml` workflow

**Why:** Orchestrates everything end-to-end. Triggered on `workflow_dispatch` and on `release/dc-v*` tag pushes. On a tag, attaches the bundle and standalone dacpac as GitHub Release assets so the next release can diff against them.

**Files:**

- Create: `.github/workflows/release-iis-dc.yaml`

- [ ] **Step 1: Create the workflow file**

```yaml
# Release IIS Package — DC
# Builds a self-contained IIS deployment bundle for DC: API + web + dacpac +
# README + schema-change report.
#
# Triggers:
#   workflow_dispatch (manual button, optional version input)
#   push of tags matching release/dc-v* (creates a GitHub Release with the bundle attached)

name: Release IIS Package — DC

on:
  workflow_dispatch:
    inputs:
      version:
        description: "Version label for the bundle (default: dev-{shortsha})"
        required: false
        type: string
  push:
    tags:
      - "release/dc-v*"

jobs:
  build:
    name: Build DC IIS bundle
    runs-on: ubuntu-latest
    permissions:
      contents: write # needed to create a Release on tag
    env:
      STATE: dc

    steps:
      - name: Checkout portal
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Resolve version
        id: ver
        run: |
          if [[ "${GITHUB_REF}" == refs/tags/release/dc-v* ]]; then
            VERSION="${GITHUB_REF#refs/tags/release/dc-v}"
          elif [ -n "${{ github.event.inputs.version }}" ]; then
            VERSION="${{ github.event.inputs.version }}"
          else
            VERSION="dev-$(git rev-parse --short HEAD)"
          fi
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"
          echo "git_sha=$(git rev-parse HEAD)" >> "$GITHUB_OUTPUT"
          echo "Resolved version: $VERSION"

      - name: Determine state-connector ref
        id: state-connector-ref
        run: |
          BRANCH="${{ github.event_name == 'pull_request' && github.head_ref || github.ref_name }}"
          FALLBACK="main"
          REPO_URL="https://x-access-token:${{ secrets.GITHUB_TOKEN }}@github.com/${{ github.repository_owner }}/sebt-self-service-portal-state-connector.git"
          if git ls-remote --exit-code --heads "$REPO_URL" "refs/heads/${BRANCH}" 1>/dev/null 2>&1; then
            echo "ref=${BRANCH}" >> "$GITHUB_OUTPUT"
          else
            echo "ref=${FALLBACK}" >> "$GITHUB_OUTPUT"
          fi

      - name: Determine dc-connector ref
        id: dc-connector-ref
        run: |
          BRANCH="${{ github.event_name == 'pull_request' && github.head_ref || github.ref_name }}"
          FALLBACK="main"
          REPO_URL="https://x-access-token:${{ secrets.GITHUB_TOKEN }}@github.com/${{ github.repository_owner }}/sebt-self-service-portal-dc-connector.git"
          if git ls-remote --exit-code --heads "$REPO_URL" "refs/heads/${BRANCH}" 1>/dev/null 2>&1; then
            echo "ref=${BRANCH}" >> "$GITHUB_OUTPUT"
          else
            echo "ref=${FALLBACK}" >> "$GITHUB_OUTPUT"
          fi

      - name: Checkout state-connector (interfaces NuGet source)
        uses: actions/checkout@v4
        with:
          repository: codeforamerica/sebt-self-service-portal-state-connector
          ref: ${{ steps.state-connector-ref.outputs.ref }}
          path: state-connector

      - name: Checkout dc-connector (DC plugin implementation)
        uses: actions/checkout@v4
        with:
          repository: codeforamerica/sebt-self-service-portal-dc-connector
          ref: ${{ steps.dc-connector-ref.outputs.ref }}
          path: dc-connector

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.200"

      - name: Setup pnpm
        uses: pnpm/action-setup@v4
        with:
          version: "10"

      - name: Setup Node 24
        uses: actions/setup-node@v4
        with:
          node-version: "24"
          cache: "pnpm"

      - name: Install dotnet-ef and sqlpackage
        run: |
          dotnet tool install --global dotnet-ef
          dotnet tool install --global Microsoft.SqlPackage
          echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

      - name: Build backend (includes state-connector NuGet pack)
        run: ./scripts/ci/build-backend.sh --configuration Release -p:BuildFrontend=false

      - name: Build DC connector (populates src/SEBT.Portal.Api/plugins-dc/)
        # The DC connector's csproj has a CopyPlugins target (AfterTargets="Build")
        # that copies its DLL output into the API's plugins-dc/ folder. We override
        # PluginDestDir to make the destination explicit (the default expects the
        # portal repo as a sibling, which is true in CI but worth being explicit).
        run: |
          dotnet build dc-connector/src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj \
            --configuration Release \
            -p:PluginDestDir="$(pwd)/src/SEBT.Portal.Api/plugins-dc" \
            --verbosity minimal

      - name: Publish API for IIS
        run: ./scripts/ci/publish-api.sh --output staging --configuration Release

      - name: Build and package frontend
        run: |
          ./.github/workflows/scripts/build-frontend.sh --production
          ./scripts/ci/package-frontend.sh --skip-build --output output/sebt-web.zip

      - name: Extract DACPAC
        run: ./scripts/ci/extract-dacpac.sh --output "output/sebt-portal-${{ steps.ver.outputs.version }}.dacpac"

      - name: Generate DACPAC report
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          ./scripts/ci/generate-dacpac-report.sh \
            --dacpac "output/sebt-portal-${{ steps.ver.outputs.version }}.dacpac" \
            --output-dir output/dacpac-report \
            --prev-tag-pattern "release/dc-v*" \
            --repo "${{ github.repository }}"

      - name: Bundle IIS package
        run: |
          ./scripts/ci/bundle-iis-package.sh \
            --api-dir staging/api \
            --web-zip output/sebt-web.zip \
            --dacpac "output/sebt-portal-${{ steps.ver.outputs.version }}.dacpac" \
            --changelog output/dacpac-report/CHANGELOG-DACPAC.md \
            --deploy-report-xml output/dacpac-report/deploy-report.xml \
            --deploy-report-html output/dacpac-report/deploy-report.html \
            --version "${{ steps.ver.outputs.version }}" \
            --git-sha "${{ steps.ver.outputs.git_sha }}" \
            --output "output/sebt-dc-iis-${{ steps.ver.outputs.version }}.zip"

      - name: Upload workflow artifact
        if: github.event_name == 'workflow_dispatch'
        uses: actions/upload-artifact@v4
        with:
          name: sebt-dc-iis-${{ steps.ver.outputs.version }}
          path: |
            output/sebt-dc-iis-${{ steps.ver.outputs.version }}.zip
            output/sebt-portal-${{ steps.ver.outputs.version }}.dacpac
          retention-days: 90

      - name: Create GitHub Release (tag push)
        if: startsWith(github.ref, 'refs/tags/release/dc-v')
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          gh release create "${GITHUB_REF#refs/tags/}" \
            "output/sebt-dc-iis-${{ steps.ver.outputs.version }}.zip" \
            "output/sebt-portal-${{ steps.ver.outputs.version }}.dacpac" \
            --title "DC v${{ steps.ver.outputs.version }}" \
            --notes-file "output/dacpac-report/CHANGELOG-DACPAC.md"
```

- [ ] **Step 2: Lint the YAML locally**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release-iis-dc.yaml'))" && echo OK`
Expected: `OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-iis-dc.yaml
git commit -m "Add release-iis-dc.yaml workflow for DC IIS bundle"
```

---

## Task 8: Wire the test runner into PR CI

**Why:** Without CI integration, the smoke tests will rot. Add a small job to `state-ci.yaml` that runs `scripts/ci/tests/run-all.sh` on PRs that touch `scripts/ci/`. Keep the change minimal to avoid scope creep into existing CI.

**Files:**

- Modify: `.github/workflows/state-ci.yaml`

- [ ] **Step 1: Read the current state-ci.yaml to find the right insertion point**

Run: `sed -n '1,30p' .github/workflows/state-ci.yaml`
Confirm: top-level `jobs:` map exists. We'll add a new job alongside `discover-states`.

- [ ] **Step 2: Add the new `ci-scripts-test` job at the end of `jobs:`**

Append to `.github/workflows/state-ci.yaml`:

```yaml
ci-scripts-test:
  name: CI scripts smoke tests
  runs-on: ubuntu-latest
  if: |
    github.event_name == 'pull_request' &&
    contains(toJson(github.event.pull_request.changed_files), 'scripts/ci/')
  steps:
    - name: Checkout
      uses: actions/checkout@v4

    - name: Run scripts/ci/tests/run-all.sh
      run: bash scripts/ci/tests/run-all.sh
```

Note: the `contains(toJson(...))` guard is best-effort. If it doesn't behave as expected on your runner version, replace with `paths:` triggering on the workflow itself, or drop the guard and run on every PR (the suite is fast).

- [ ] **Step 3: Lint the YAML**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/state-ci.yaml'))" && echo OK`
Expected: `OK`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/state-ci.yaml
git commit -m "Run scripts/ci/tests/run-all.sh on PRs that touch scripts/ci/"
```

---

## Task 9: Push branch and verify with a manual workflow_dispatch run

**Why:** The release workflow is the integration test. We can't fully verify it locally; we need to push the branch, trigger `workflow_dispatch`, and check the artifact. The first run is also the only chance to validate the first-run DACPAC path against real GitHub Releases (no prior release exists for `release/dc-v*` yet).

- [ ] **Step 1: Push the branch**

```bash
git push -u origin chore/iis-release-package
```

- [ ] **Step 2: Open a PR**

Use the existing PR template per `CLAUDE.md`. Title: `chore: add IIS release package CI workflow`. Body summarizes the spec link, the new scripts/templates/workflow, and notes that the workflow is testable via `workflow_dispatch`.

- [ ] **Step 3: Trigger the workflow manually**

Via the GitHub UI or `gh`: `gh workflow run release-iis-dc.yaml --ref chore/iis-release-package -f version=dev-test`

- [ ] **Step 4: Verify the artifact**

Once the run completes, download `sebt-dc-iis-dev-test.zip` from the workflow run page. Extract it on macOS and verify:

- [ ] `README.md` exists with version `dev-test` substituted in.
- [ ] `CHANGELOG-DACPAC.md` says "Initial release — full schema (no prior baseline)" since no `release/dc-v*` tag exists yet.
- [ ] `api/` has `SEBT.Portal.Api.dll`, `plugins-dc/` (with DLLs), `appsettings.prod.example.json`, and `web.config` containing `stdoutLogEnabled="true"`.
- [ ] `web/` has `web.config`, `src/SEBT.Portal.Web/server.js`, no symlinks (`find web -type l` returns nothing).
- [ ] `dacpac/sebt-portal-dev-test.dacpac` exists.

If any of those fail, fix in this branch and re-run before merging.

- [ ] **Step 5: After merge, cut the first real release**

After this PR merges, push tag `release/dc-v0.1.0` (or whatever the agreed first version is). Verify that the workflow attaches the zip and the dacpac as Release assets. The next release will then have a baseline to diff against, exercising the delta path of `generate-dacpac-report.sh` for real.

---

## Self-review checklist (for the implementer)

Before marking the plan complete, verify:

- [ ] All scripts have `set -e` and `set -u` at the top.
- [ ] All scripts in `scripts/ci/` are executable (`ls -l` shows the `x` bit).
- [ ] `scripts/ci/tests/run-all.sh` passes locally.
- [ ] `.github/workflows/release-iis-dc.yaml` parses as valid YAML.
- [ ] No script writes to `$PROJECT_ROOT` outside `output/` or its `--output` argument.
- [ ] No hardcoded secrets in any committed file.
- [ ] The README template contains no `{{...}}` placeholders that the bundle script doesn't resolve.
