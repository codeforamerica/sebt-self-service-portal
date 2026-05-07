#!/usr/bin/env bash
# Tiny assertion helpers shared by scripts/ci/tests/*_test.sh.
# Source this file at the top of each test:  source "$(dirname "$0")/_assert.sh"
# Each assertion prints to stderr and exits non-zero on failure (the parent test fails fast).

set -euo pipefail

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
  local listing
  listing=$(unzip -l "$zip_path")
  if ! grep -qF -- "$entry" <<<"$listing"; then
    echo "ASSERT FAIL: expected zip '$zip_path' to contain entry: $entry" >&2
    echo "$listing" >&2
    exit 1
  fi
}
