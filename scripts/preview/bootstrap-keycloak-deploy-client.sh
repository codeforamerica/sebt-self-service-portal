#!/usr/bin/env bash
# One-time: ensure the sebt-preview-deploy client exists on a live Keycloak
# realm. Needed because --import-realm does not overwrite an existing Postgres
# realm after the first start.
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
IFS=$'\t' read -r DEPLOY_CLIENT_ID DEPLOY_CLIENT_SECRET < <(keycloak_deploy_credentials)

log_info "Checking Keycloak deploy client ${DEPLOY_CLIENT_ID} at ${KEYCLOAK_HOSTNAME}"

if keycloak_deploy_token "${KEYCLOAK_HOSTNAME}" >/dev/null 2>&1; then
  log_info "Deploy client already works (client_credentials OK); nothing to do"
  exit 0
fi

log_info "Deploy client missing or unauthorized; creating via bootstrap admin"
ADMIN_TOKEN="$(keycloak_admin_token "${KEYCLOAK_HOSTNAME}")"

EXISTING_UUID=""
uuid_rc=0
EXISTING_UUID="$(keycloak_client_uuid "${KEYCLOAK_HOSTNAME}" "${ADMIN_TOKEN}" "${DEPLOY_CLIENT_ID}")" || uuid_rc=$?
if [ "${uuid_rc}" -eq 0 ] && [ -n "${EXISTING_UUID}" ]; then
  log_info "Client ${DEPLOY_CLIENT_ID} already exists (${EXISTING_UUID}); will refresh secret and roles"
  CLIENT_UUID="${EXISTING_UUID}"
else
  payload="$(mktemp)"
  body="$(mktemp)"
  jq -n \
    --arg client_id "${DEPLOY_CLIENT_ID}" \
    --arg secret "${DEPLOY_CLIENT_SECRET}" \
    '{
      clientId: $client_id,
      name: "SEBT Preview deploy (Admin API)",
      enabled: true,
      protocol: "openid-connect",
      publicClient: false,
      secret: $secret,
      serviceAccountsEnabled: true,
      standardFlowEnabled: false,
      implicitFlowEnabled: false,
      directAccessGrantsEnabled: false,
      frontchannelLogout: false,
      redirectUris: [],
      webOrigins: []
    }' >"${payload}"

  http_code="$(keycloak_admin_request POST \
    "${KEYCLOAK_HOSTNAME}/admin/realms/sebt/clients" \
    "${ADMIN_TOKEN}" "${body}" "${payload}")"
  rm -f "${payload}"

  if [ "${http_code}" != "201" ] && [ "${http_code}" != "200" ]; then
    log_error "Failed to create deploy client (HTTP ${http_code}): $(head -c 500 "${body}")"
    rm -f "${body}"
    exit 1
  fi
  rm -f "${body}"

  CLIENT_UUID="$(keycloak_client_uuid "${KEYCLOAK_HOSTNAME}" "${ADMIN_TOKEN}" "${DEPLOY_CLIENT_ID}")"
  log_info "Created deploy client ${DEPLOY_CLIENT_ID} (${CLIENT_UUID})"
fi

# Ensure client secret matches the expected preview default / override.
current="$(keycloak_get_client "${KEYCLOAK_HOSTNAME}" "${ADMIN_TOKEN}" "${CLIENT_UUID}")"
updated="$(echo "${current}" | jq --arg secret "${DEPLOY_CLIENT_SECRET}" \
  '.secret = $secret | .serviceAccountsEnabled = true | .publicClient = false')"
keycloak_put_client "${KEYCLOAK_HOSTNAME}" "${ADMIN_TOKEN}" "${CLIENT_UUID}" "${updated}"

# Resolve service-account user for this client.
sa_body="$(mktemp)"
http_code="$(keycloak_admin_request GET \
  "${KEYCLOAK_HOSTNAME}/admin/realms/sebt/clients/${CLIENT_UUID}/service-account-user" \
  "${ADMIN_TOKEN}" "${sa_body}")"
if [ "${http_code}" != "200" ]; then
  log_error "Failed to load service-account user (HTTP ${http_code}): $(head -c 500 "${sa_body}")"
  rm -f "${sa_body}"
  exit 1
fi
SA_USER_ID="$(jq -r '.id // empty' "${sa_body}")"
rm -f "${sa_body}"
if [ -z "${SA_USER_ID}" ]; then
  log_error "Service-account user id missing for ${DEPLOY_CLIENT_ID}"
  exit 1
fi

# Resolve realm-management client + desired roles.
rm_clients_body="$(mktemp)"
http_code="$(curl -sS -o "${rm_clients_body}" -w "%{http_code}" \
  -G "${KEYCLOAK_HOSTNAME}/admin/realms/sebt/clients" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  --data-urlencode "clientId=realm-management")" || true
if [ "${http_code}" != "200" ]; then
  log_error "Failed to look up realm-management client (HTTP ${http_code})"
  rm -f "${rm_clients_body}"
  exit 1
fi
RM_CLIENT_UUID="$(jq -r 'map(select(.clientId == "realm-management")) | .[0].id // empty' "${rm_clients_body}")"
rm -f "${rm_clients_body}"
if [ -z "${RM_CLIENT_UUID}" ]; then
  log_error "realm-management client not found in sebt realm"
  exit 1
fi

roles_body="$(mktemp)"
http_code="$(keycloak_admin_request GET \
  "${KEYCLOAK_HOSTNAME}/admin/realms/sebt/clients/${RM_CLIENT_UUID}/roles" \
  "${ADMIN_TOKEN}" "${roles_body}")"
if [ "${http_code}" != "200" ]; then
  log_error "Failed to list realm-management roles (HTTP ${http_code})"
  rm -f "${roles_body}"
  exit 1
fi

role_payload="$(mktemp)"
jq '[.[] | select(.name == "manage-clients" or .name == "view-clients" or .name == "query-clients")]' \
  "${roles_body}" >"${role_payload}"
rm -f "${roles_body}"

if [ "$(jq 'length' "${role_payload}")" -lt 1 ]; then
  log_error "Required realm-management roles not found"
  rm -f "${role_payload}"
  exit 1
fi

map_body="$(mktemp)"
http_code="$(keycloak_admin_request POST \
  "${KEYCLOAK_HOSTNAME}/admin/realms/sebt/users/${SA_USER_ID}/role-mappings/clients/${RM_CLIENT_UUID}" \
  "${ADMIN_TOKEN}" "${map_body}" "${role_payload}")"
rm -f "${role_payload}"
if [ "${http_code}" != "204" ] && [ "${http_code}" != "200" ]; then
  log_error "Failed to assign service-account roles (HTTP ${http_code}): $(head -c 500 "${map_body}")"
  rm -f "${map_body}"
  exit 1
fi
rm -f "${map_body}"

if ! keycloak_deploy_token "${KEYCLOAK_HOSTNAME}" >/dev/null; then
  log_error "Deploy client still cannot obtain a token after bootstrap"
  exit 1
fi

log_info "Keycloak deploy client ${DEPLOY_CLIENT_ID} is ready"
