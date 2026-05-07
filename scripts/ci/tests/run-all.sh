#!/usr/bin/env bash
# Runs every *_test.sh in this directory. Exits non-zero on the first failure.
set -euo pipefail

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
