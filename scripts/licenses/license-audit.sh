#!/bin/bash
# Script for running an audit of direct dependency licenses and output to CSV
#
# Usage
#   ./scripts/license-audit.sh

set -e  # Exit on error
set -u  # Exit on undefined variable

#dotnet tool run nuget-license \
#  --input SEBT.Portal.sln \
#  -o json \
#  | jq .

dotnet tool run nuget-license \
  --input SEBT.Portal.sln \
  -o json \
  -a scripts/licenses/allowed-licenses.json \
  -mapping scripts/licenses/license-mappings.json \
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
    | @csv' > scripts/licenses/backend-dependencies.csv
