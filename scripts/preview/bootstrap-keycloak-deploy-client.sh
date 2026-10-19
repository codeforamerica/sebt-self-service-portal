#!/usr/bin/env bash
# One-time / manual: ensure the sebt-preview-deploy client exists on a live
# Keycloak realm. Preview deploy and destroy call the same helper automatically
# because --import-realm does not overwrite an existing Postgres realm.
#
# Uses the bootstrap admin secret (master realm). If that returns 401, fix
# admin credential drift first, or recreate the Keycloak DB / re-import.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"
# shellcheck source=keycloak.sh
source "${SCRIPT_DIR}/keycloak.sh"

usage() {
  cat <<'EOF'
Usage: bootstrap-keycloak-deploy-client.sh

Environment:
  PREVIEW_DOMAIN / DOMAIN            Public domain (default auth host derived)
  PREVIEW_KEYCLOAK_HOSTNAME          Keycloak base URL (default: https://auth.<DOMAIN>)
  PREVIEW_KEYCLOAK_ADMIN_SECRET_ID   Bootstrap admin Secrets Manager id/ARN
  PREVIEW_KEYCLOAK_ADMIN_BYPASS_SECRET_ID  ALB /admin* bypass header secret (default name)
  PREVIEW_KEYCLOAK_ADMIN_BYPASS_HEADER     Bypass header value override (skips SM lookup)
  PREVIEW_KEYCLOAK_DEPLOY_CLIENT_ID  Deploy client id (default: sebt-preview-deploy)
  PREVIEW_KEYCLOAK_DEPLOY_CLIENT_SECRET Deploy client secret (default: realm secret)
EOF
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
  usage
  exit 0
fi

preview_requirements
require_command curl

DOMAIN="$(resolve_preview_domain)"
KEYCLOAK_HOSTNAME="${PREVIEW_KEYCLOAK_HOSTNAME:-https://auth.${DOMAIN}}"

log_info "Checking Keycloak deploy client at ${KEYCLOAK_HOSTNAME}"
ensure_keycloak_deploy_client "${KEYCLOAK_HOSTNAME}"
