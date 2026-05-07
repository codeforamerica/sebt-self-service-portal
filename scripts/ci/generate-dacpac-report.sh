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
#   deploy-report.xml     raw DeployReport (placeholder on first-run)
#   deploy-report.html    rendered version (placeholder on first-run)
#
# First-run case: when no tag matching --prev-tag-pattern exists in the repo,
# CHANGELOG-DACPAC.md is written with an "Initial release" message and
# placeholder DeployReport files are produced so downstream packaging can use a
# stable file contract. The script exits 0.

set -euo pipefail

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

write_initial_release_reports() {
  local reason="$1"

  cat > "$OUT_DIR/deploy-report.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<DeployReport>
  <Alert Type="InitialRelease">No previous DACPAC baseline was available.</Alert>
  <Message>$reason</Message>
</DeployReport>
EOF

  cat > "$OUT_DIR/deploy-report.html" <<EOF
<!doctype html>
<html><head><meta charset="utf-8"><title>DeployReport — Initial release</title>
<style>body{font-family:system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1rem}</style>
</head><body>
<h1>DACPAC DeployReport</h1>
<p><strong>Initial release:</strong> no previous DACPAC baseline was available.</p>
<p>$reason</p>
</body></html>
EOF
}

# Resolve previous release tag via gh.
log_info "Looking for previous release matching: $PREV_TAG_PATTERN"
if ! command -v gh >/dev/null 2>&1; then
  log_warning "gh CLI not found — treating as first run"
  write_initial_release_changelog "gh CLI unavailable"
  write_initial_release_reports "GitHub CLI was unavailable, so no previous release baseline could be inspected."
  log_success "Done."
  exit 0
fi

GH_REPO_FLAG=()
if [ -n "$REPO" ]; then
  GH_REPO_FLAG=(--repo "$REPO")
fi

# Bash 3.2 (macOS default) treats `"${arr[@]}"` as unbound under `set -u` when
# the array is empty. Use the `${arr[@]+"${arr[@]}"}` idiom to expand-only-if-set.
# `gh release list` does not support globs natively, so we filter after.
PREV_TAG="$(gh release list ${GH_REPO_FLAG[@]+"${GH_REPO_FLAG[@]}"} --limit 100 --json tagName --jq '.[].tagName' \
  | grep -E "^${PREV_TAG_PATTERN//\*/.*}$" \
  | head -n 1 || true)"

if [ -z "$PREV_TAG" ]; then
  write_initial_release_changelog "no matching prior release"
  write_initial_release_reports "No release matching $PREV_TAG_PATTERN was found."
  log_success "Done."
  exit 0
fi

log_info "Previous release: $PREV_TAG"

# Download the previous DACPAC asset.
PREV_DIR="$(mktemp -d)"
trap 'rm -rf "$PREV_DIR"' EXIT
log_info "Downloading previous DACPAC from $PREV_TAG"
gh release download "$PREV_TAG" ${GH_REPO_FLAG[@]+"${GH_REPO_FLAG[@]}"} --dir "$PREV_DIR" --pattern "*.dacpac" || {
  log_warning "No DACPAC asset on $PREV_TAG — treating as first run"
  write_initial_release_changelog "previous release had no DACPAC asset"
  write_initial_release_reports "Previous release $PREV_TAG did not include a DACPAC asset."
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
