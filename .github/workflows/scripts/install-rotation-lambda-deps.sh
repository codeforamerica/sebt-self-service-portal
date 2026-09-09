#!/bin/bash
# Installs the DB rotation Lambda's Python dependencies. pymssql ships native
# extensions, so it must be installed for the Lambda's target platform
# (manylinux, x86_64, CPython 3.12) rather than the host OS. Shared by
# deploy-ecr.yaml and plan.yaml so the install command can't drift between
# the dc/co deploy jobs and the plan job.
#
# Usage
#   ./.github/workflows/scripts/install-rotation-lambda-deps.sh

set -e  # Exit on error
set -u  # Exit on undefined variable

SCRIPT_DIR=$( cd -- "$( dirname -- "$0" )" &> /dev/null && pwd -P )
LAMBDA_DIR="$SCRIPT_DIR/../../../tofu/modules/sebt_database/lambda"

pip install \
  --platform manylinux_2_28_x86_64 \
  --target "$LAMBDA_DIR" \
  --implementation cp \
  --python-version 3.12 \
  --only-binary :all: \
  --upgrade \
  --quiet \
  -r "$LAMBDA_DIR/requirements.txt"
