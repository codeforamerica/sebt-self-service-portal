#!/usr/bin/env bash
# Generates/updates the C# API client for the CBMS SEBT API using Kiota.
# Re-run this script whenever the OpenAPI spec (api-spec.yaml) changes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SPEC_FILE="$SCRIPT_DIR/api-spec.yaml"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Ensure .NET tools (kiota) are available
dotnet tool restore --tool-manifest "$REPO_ROOT/.config/dotnet-tools.json"

# --clean-output safely wipes only the Generated/ subdirectory,
# so no backup/restore of .csproj or openapi/ is needed.
dotnet kiota generate \
  --language CSharp \
  --openapi "$SPEC_FILE" \
  --output "$PROJECT_DIR/Generated" \
  --class-name CbmsSebtApiClient \
  --namespace-name SEBT.Portal.StatePlugins.CO.CbmsApi \
  --exclude-backward-compatible \
  --clean-output \
  --log-level Warning

echo "Client generated in $PROJECT_DIR/Generated"
