#!/usr/bin/env bash
# Upserts the realm-JSON fixture users into the live shared preview Keycloak.
# --import-realm skips an existing realm, so later fixture additions never land.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"
# shellcheck source=keycloak.sh
source "${SCRIPT_DIR}/keycloak.sh"

DEFAULT_REALM_FILE="${SCRIPT_DIR}/../../docker/keycloak/sebt-realm.preview.json"

usage() {
  cat <<'EOF_USAGE'
Usage: sync-keycloak-fixture-users.sh [--dry-run] [REALM_JSON]

Creates missing fixture users and updates existing ones (profile fields and
password). Never deletes users. --dry-run only looks users up and prints the plan.
REALM_JSON defaults to docker/keycloak/sebt-realm.preview.json.

Also sets the sebt realm's unmanaged attribute policy to ADMIN_EDIT when it is
stricter: without it the Admin API drops the fixtures' phone and verification
attributes, so the portal cannot match a login to its persona.

Environment:
  PREVIEW_DOMAIN / DOMAIN            Public domain (default auth host derived)
  PREVIEW_KEYCLOAK_HOSTNAME          Keycloak base URL (default: https://auth.<DOMAIN>)
  PREVIEW_KEYCLOAK_ADMIN_SECRET_ID   Bootstrap admin Secrets Manager id/ARN
  PREVIEW_KEYCLOAK_ADMIN_BYPASS_SECRET_ID  ALB /admin* bypass header secret (default name)
  PREVIEW_KEYCLOAK_ADMIN_BYPASS_HEADER     Bypass header value override (skips SM lookup)
EOF_USAGE
}

# Users that carry credentials, excluding service accounts. One JSON object per line.
fixture_users() {
  local realm_file="$1"
  jq -c '.users[]
    | select(.credentials != null and (.username | startswith("service-account-") | not))' \
    "${realm_file}"
}

# User representation without credentials, for PUT on an existing user.
user_without_credentials() {
  jq -c '{username, email, emailVerified, enabled, firstName, lastName, attributes}
    | with_entries(select(.value != null))'
}

# First password credential, as the body for reset-password.
password_credential() {
  jq -c '[.credentials[] | select(.type == "password")] | .[0] // empty'
}

# Echoes the existing user's id, or nothing when the user does not exist.
lookup_user_id() {
  local keycloak_hostname="$1" token="$2" username="$3"
  local body http_code encoded

  body="$(mktemp)"
  encoded="$(jq -rn --arg u "${username}" '$u | @uri')"
  http_code="$(keycloak_admin_request GET \
    "${keycloak_hostname}/admin/realms/sebt/users?username=${encoded}&exact=true" \
    "${token}" "${body}")"
  if [ "${http_code}" != "200" ]; then
    log_error "Failed to look up user ${username} (HTTP ${http_code}): $(head -c 300 "${body}")"
    rm -f "${body}"
    return 1
  fi
  jq -r --arg u "${username}" '[.[] | select(.username == $u)] | .[0].id // empty' "${body}"
  rm -f "${body}"
}

# Sends JSON to the Admin API; fails unless the status is 200, 201 or 204.
send_json() {
  local method="$1" url="$2" token="$3" json="$4" description="$5"
  local payload body http_code

  payload="$(mktemp)"
  body="$(mktemp)"
  printf '%s' "${json}" >"${payload}"
  http_code="$(keycloak_admin_request "${method}" "${url}" "${token}" "${body}" "${payload}")"
  rm -f "${payload}"
  case "${http_code}" in
    200 | 201 | 204) rm -f "${body}" ;;
    *)
      log_error "Failed to ${description} (HTTP ${http_code}): $(head -c 300 "${body}")"
      rm -f "${body}"
      return 1
      ;;
  esac
}

# The fixtures' phone and verification attributes are not declared in the realm's user profile,
# and Keycloak's Admin API silently drops undeclared attributes unless admins may edit them.
ensure_admin_can_edit_unmanaged_attributes() {
  local keycloak_hostname="$1" token="$2" dry_run="$3"
  local profile_url body http_code policy updated_profile

  profile_url="${keycloak_hostname}/admin/realms/sebt/users/profile"
  body="$(mktemp)"
  http_code="$(keycloak_admin_request GET "${profile_url}" "${token}" "${body}")"
  if [ "${http_code}" != "200" ]; then
    log_error "Failed to read the sebt user profile (HTTP ${http_code}): $(head -c 300 "${body}")"
    rm -f "${body}"
    return 1
  fi
  policy="$(jq -r '.unmanagedAttributePolicy // "DISABLED"' "${body}")"
  updated_profile="$(jq -c '.unmanagedAttributePolicy = "ADMIN_EDIT"' "${body}")"
  rm -f "${body}"

  if [ "${policy}" = "ADMIN_EDIT" ] || [ "${policy}" = "ENABLED" ]; then
    return
  fi

  if [ "${dry_run}" = "true" ]; then
    log_info "user profile: would set unmanagedAttributePolicy to ADMIN_EDIT (now ${policy})"
    return
  fi

  send_json PUT "${profile_url}" "${token}" "${updated_profile}" "update the sebt user profile"
  log_info "user profile: set unmanagedAttributePolicy to ADMIN_EDIT (was ${policy})"
}

sync_user() {
  local keycloak_hostname="$1" token="$2" user_json="$3" dry_run="$4"
  local username user_id users_url

  username="$(jq -r '.username' <<<"${user_json}")"
  users_url="${keycloak_hostname}/admin/realms/sebt/users"
  user_id="$(lookup_user_id "${keycloak_hostname}" "${token}" "${username}")"

  if [ -z "${user_id}" ]; then
    if [ "${dry_run}" = "true" ]; then
      log_info "${username}: would create"
      return
    fi
    send_json POST "${users_url}" "${token}" "${user_json}" "create user ${username}"
    log_info "${username}: created"
    return
  fi

  if [ "${dry_run}" = "true" ]; then
    log_info "${username}: would update"
    return
  fi
  send_json PUT "${users_url}/${user_id}" "${token}" \
    "$(user_without_credentials <<<"${user_json}")" "update user ${username}"
  send_json PUT "${users_url}/${user_id}/reset-password" "${token}" \
    "$(password_credential <<<"${user_json}")" "reset password for ${username}"
  log_info "${username}: updated"
}

main() {
  local dry_run="false" realm_file="${DEFAULT_REALM_FILE}" arg
  for arg in "$@"; do
    case "${arg}" in
      -h | --help)
        usage
        return 0
        ;;
      --dry-run) dry_run="true" ;;
      *) realm_file="${arg}" ;;
    esac
  done

  if [ ! -f "${realm_file}" ]; then
    log_error "Realm file not found: ${realm_file}"
    return 1
  fi

  preview_requirements
  require_command curl

  local domain keycloak_hostname token user_json
  domain="$(resolve_preview_domain)"
  keycloak_hostname="${PREVIEW_KEYCLOAK_HOSTNAME:-https://auth.${domain}}"

  log_info "Syncing fixture users from ${realm_file} to ${keycloak_hostname} (dry-run: ${dry_run})"
  token="$(keycloak_admin_token "${keycloak_hostname}")"

  ensure_admin_can_edit_unmanaged_attributes "${keycloak_hostname}" "${token}" "${dry_run}"
  while IFS= read -r user_json; do
    sync_user "${keycloak_hostname}" "${token}" "${user_json}" "${dry_run}"
  done < <(fixture_users "${realm_file}")
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  main "$@"
fi
