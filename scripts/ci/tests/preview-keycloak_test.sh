#!/usr/bin/env bash
# Unit tests for scripts/preview/keycloak.sh helpers that do not need AWS.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$PROJECT_ROOT/scripts/ci/tests/_assert.sh"
# shellcheck source=../../preview/common.sh
source "$PROJECT_ROOT/scripts/preview/common.sh"
# shellcheck source=../../preview/keycloak.sh
source "$PROJECT_ROOT/scripts/preview/keycloak.sh"

# GitHub Actions interpolates missing secrets/vars as empty strings.
PREVIEW_KEYCLOAK_DEPLOY_CLIENT_ID=""
PREVIEW_KEYCLOAK_DEPLOY_CLIENT_SECRET=""
PREVIEW_KEYCLOAK_DEPLOY_SECRET_ID=""
IFS=$'\t' read -r client_id client_secret < <(keycloak_deploy_credentials)
assert_eq "${client_id}" "sebt-preview-deploy"
assert_eq "${client_secret}" "sebt-preview-deploy-secret"

payload="$(keycloak_deploy_client_create_payload sebt-preview-deploy sebt-preview-deploy-secret)"
assert_eq "$(echo "${payload}" | jq -r '.clientId')" "sebt-preview-deploy"
assert_eq "$(echo "${payload}" | jq -r '.secret')" "sebt-preview-deploy-secret"
assert_eq "$(echo "${payload}" | jq -r '.publicClient')" "false"
assert_eq "$(echo "${payload}" | jq -r '.serviceAccountsEnabled')" "true"
assert_eq "$(echo "${payload}" | jq -r '.clientAuthenticatorType')" "client-secret"
assert_eq "$(echo "${payload}" | jq -r '.standardFlowEnabled')" "false"

register_fn="$(sed -n '/^ensure_keycloak_preview_host_redirects()/,/^}/p' \
  "$PROJECT_ROOT/scripts/preview/keycloak.sh")"
remove_fn="$(sed -n '/^remove_keycloak_preview_host_redirects()/,/^}/p' \
  "$PROJECT_ROOT/scripts/preview/keycloak.sh")"
wrapper="$(cat "$PROJECT_ROOT/scripts/preview/bootstrap-keycloak-deploy-client.sh")"

echo "${register_fn}" | grep -q 'ensure_keycloak_deploy_client "${keycloak_hostname}"' \
  || { echo "ASSERT FAIL: register path does not seed the deploy client" >&2; exit 1; }
echo "${remove_fn}" | grep -q 'ensure_keycloak_deploy_client "${keycloak_hostname}"' \
  || { echo "ASSERT FAIL: remove path does not seed the deploy client" >&2; exit 1; }
echo "${wrapper}" | grep -q 'ensure_keycloak_deploy_client "${KEYCLOAK_HOSTNAME}"' \
  || { echo "ASSERT FAIL: bootstrap script does not call ensure_keycloak_deploy_client" >&2; exit 1; }

echo "preview-keycloak_test: OK"
