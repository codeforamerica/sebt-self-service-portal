#!/bin/sh
# Applies dotnet format to staged .cs files only.
# Run this before committing to fix any formatting issues, then re-stage the fixes.

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$PROJECT_ROOT"

STAGED_CS_FILES=$(git diff --cached --name-only --diff-filter=ACMR | grep '\.cs$' || true)

if [ -z "$STAGED_CS_FILES" ]; then
  echo "No staged .cs files to format."
  exit 0
fi

INCLUDE_FLAGS=""
for f in $STAGED_CS_FILES; do
  INCLUDE_FLAGS="$INCLUDE_FLAGS --include $f"
  echo "  $f"
done

echo "Formatting staged .cs files..."

dotnet format whitespace SEBT.Portal.sln $INCLUDE_FLAGS --verbosity quiet --no-restore

echo "Done. Re-stage any formatting fixes before committing."
