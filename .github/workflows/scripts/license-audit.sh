#!/bin/bash
# Script for running an audit of direct dependency licenses and output to CSV
#
# Usage
#   ./scripts/license-audit.sh

set -e  # Exit on error
set -u  # Exit on undefined variable

SCRIPT_DIR=$( cd -- "$( dirname -- "$0" )" &> /dev/null && pwd -P )
ALLOWED_LICENSES="$SCRIPT_DIR/licenses/allowed-licenses.json"
LICENSE_MAPPINGS="$SCRIPT_DIR/licenses/license-mappings.json"
BACKEND_OUTPUT="output/backend-dependencies.csv"

# Create a directory for output if needed
mkdir -p output

# Ensure the nuget-license tool is installed
dotnet tool restore

# Get license information for all NuGet packages,
# output as JSON. Use jq to extract relevant 
# attributes and output as a CSV into a file.
dotnet tool run nuget-license \
  --input SEBT.Portal.sln \
  -o json \
  -a $ALLOWED_LICENSES \
  -mapping $LICENSE_MAPPINGS \
  | jq -r '
    # 1. Header Row Filter
    ["name", "license", "scope", "coordinate", "package", "version", "errors"],
    
    # 2. Data Rows Filter
    (.[] | [
      .PackageId,
      .License,
      "direct",
      "\(.PackageId):\(.PackageVersion)",
      .PackageId,
      .PackageVersion, 
      ((.ValidationErrors // []) | map("ERROR: \(.Error)") | join("; "))
    ]) 
    | @csv' > $BACKEND_OUTPUT
