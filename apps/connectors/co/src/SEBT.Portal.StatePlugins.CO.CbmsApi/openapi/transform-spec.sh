#!/usr/bin/env bash
# Transforms the original CBMS API spec (api-spec.original.yaml) into a version
# with descriptive schema names (api-spec.yaml) for Kiota client generation.
#
# Usage: ./transform-spec.sh
#   Reads:  api-spec.original.yaml (from same directory as this script)
#   Writes: api-spec.yaml (to same directory as this script)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INPUT_FILE="$SCRIPT_DIR/api-spec.original.yaml"
OUTPUT_FILE="$SCRIPT_DIR/api-spec.yaml"

if [ ! -f "$INPUT_FILE" ]; then
  echo "Error: $INPUT_FILE not found." >&2
  exit 1
fi

python3 "$SCRIPT_DIR/transform_spec.py" "$INPUT_FILE" "$OUTPUT_FILE"
