#!/usr/bin/env bash
# Keycloak Admin helpers for CO preview OIDC redirect registration.
#
# Keycloak 26+ only accepts path-trailing wildcards in Valid Redirect URIs
# (https://host.example/*), not hostname wildcards (https://*.example/*).
# Preview deploys therefore register each pr-N host explicitly on the shared
# clients, and destroy removes them.

# Default Secrets Manager secret id from tofu/modules/sebt_keycloak
# (prefix = ${project}-${state}-${environment}-keycloak-admin). Prefer setting
# PREVIEW_KEYCLOAK_ADMIN_SECRET_ID to the tofu output ARN
# (preview_keycloak_admin_secret_arn) in CI.
resolve_keycloak_admin_secret_id() {
  if [ -n "${PREVIEW_KEYCLOAK_ADMIN_SECRET_ID:-}" ]; then
    echo "${PREVIEW_KEYCLOAK_ADMIN_SECRET_ID}"
    return
  fi
  echo "sebt-portal-co-development-keycloak-admin"
}

keycloak_admin_credentials() {
  local secret_id
  secret_id="$(resolve_keycloak_admin_secret_id)"

  aws secretsmanager get-secret-value \
    --secret-id "${secret_id}" \
    --query SecretString \
    --output text
}

# Mint an admin access token. Password is written to a temp form body so it
# does not appear on the curl process argv.
keycloak_admin_token() {
  local keycloak_hostname="$1"
  local credentials username password token http_code body form

  credentials="$(keycloak_admin_credentials)"
  username="$(echo "${credentials}" | jq -r '.username')"
  password="$(echo "${credentials}" | jq -r '.password')"

  if [ -z "${username}" ] || [ "${username}" = "null" ] \
    || [ -z "${password}" ] || [ "${password}" = "null" ]; then
    log_error "Keycloak admin secret missing username/password"
    return 1
  fi

  body="$(mktemp)"
  form="$(mktemp)"
  jq -nr \
    --arg user "${username}" \
    --arg pass "${password}" \
    '"grant_type=password&client_id=admin-cli&username=\($user|@uri)&password=\($pass|@uri)"' \
    >"${form}"

  http_code="$(curl -sS -o "${body}" -w "%{http_code}" \
    -X POST "${keycloak_hostname}/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-binary @"${form}")" || true
  rm -f "${form}"

  if [ "${http_code}" != "200" ]; then
    log_error "Failed to obtain Keycloak admin token (HTTP ${http_code})"
    rm -f "${body}"
    return 1
  fi

  token="$(jq -r '.access_token // empty' "${body}")"
  rm -f "${body}"

  if [ -z "${token}" ]; then
    log_error "Keycloak admin token response missing access_token"
    return 1
  fi

  echo "${token}"
}

# Perform an authenticated Keycloak Admin API request.
# Writes response body to $5 (outfile). Echoes HTTP status code on stdout.
# Usage: keycloak_admin_request METHOD URL TOKEN OUTFILE [PAYLOAD_FILE]
keycloak_admin_request() {
  local method="$1"
  local url="$2"
  local token="$3"
  local outfile="$4"
  local payload_file="${5:-}"
  local http_code

  if [ -n "${payload_file}" ]; then
    http_code="$(curl -sS -o "${outfile}" -w "%{http_code}" \
      -X "${method}" "${url}" \
      -H "Authorization: Bearer ${token}" \
      -H "Content-Type: application/json" \
      --data-binary @"${payload_file}")" || true
  else
    http_code="$(curl -sS -o "${outfile}" -w "%{http_code}" \
      -X "${method}" "${url}" \
      -H "Authorization: Bearer ${token}")" || true
  fi

  echo "${http_code}"
}

keycloak_client_uuid() {
  local keycloak_hostname="$1"
  local token="$2"
  local client_id="$3"
  local body http_code uuid

  body="$(mktemp)"
  http_code="$(curl -sS -o "${body}" -w "%{http_code}" \
    -G "${keycloak_hostname}/admin/realms/sebt/clients" \
    -H "Authorization: Bearer ${token}" \
    --data-urlencode "clientId=${client_id}")" || true

  if [ "${http_code}" = "401" ]; then
    rm -f "${body}"
    return 2
  fi

  if [ "${http_code}" != "200" ]; then
    log_error "Failed to look up Keycloak client ${client_id} (HTTP ${http_code})"
    rm -f "${body}"
    return 1
  fi

  uuid="$(jq -r --arg client_id "${client_id}" \
    'map(select(.clientId == $client_id)) | .[0].id // empty' "${body}")"
  rm -f "${body}"

  if [ -z "${uuid}" ]; then
    log_error "Keycloak client not found: ${client_id}"
    return 1
  fi

  echo "${uuid}"
}

keycloak_get_client() {
  local keycloak_hostname="$1"
  local token="$2"
  local client_uuid="$3"
  local body http_code

  body="$(mktemp)"
  http_code="$(keycloak_admin_request GET \
    "${keycloak_hostname}/admin/realms/sebt/clients/${client_uuid}" \
    "${token}" "${body}")"

  if [ "${http_code}" = "401" ]; then
    rm -f "${body}"
    return 2
  fi

  if [ "${http_code}" != "200" ]; then
    log_error "Failed to GET Keycloak client ${client_uuid} (HTTP ${http_code})"
    rm -f "${body}"
    return 1
  fi

  cat "${body}"
  rm -f "${body}"
}

keycloak_put_client() {
  local keycloak_hostname="$1"
  local token="$2"
  local client_uuid="$3"
  local client_json="$4"
  local body payload http_code

  body="$(mktemp)"
  payload="$(mktemp)"
  printf '%s' "${client_json}" >"${payload}"

  http_code="$(keycloak_admin_request PUT \
    "${keycloak_hostname}/admin/realms/sebt/clients/${client_uuid}" \
    "${token}" "${body}" "${payload}")"
  rm -f "${payload}"

  if [ "${http_code}" = "401" ]; then
    rm -f "${body}"
    return 2
  fi

  # Keycloak returns 204 No Content on success.
  if [ "${http_code}" != "204" ] && [ "${http_code}" != "200" ]; then
    log_error "Failed to PUT Keycloak client ${client_uuid} (HTTP ${http_code}): $(head -c 500 "${body}")"
    rm -f "${body}"
    return 1
  fi

  rm -f "${body}"
}

# Apply add|remove of a preview host on one client representation (stdout).
keycloak_mutate_client_preview_host() {
  local action="$1"
  local web_host="$2"
  local client_json="$3"
  local redirect origin logout

  redirect="https://${web_host}/*"
  origin="https://${web_host}"
  logout="https://${web_host}/*"

  if [ "${action}" = "add" ]; then
    echo "${client_json}" | jq \
      --arg redirect "${redirect}" \
      --arg origin "${origin}" \
      --arg logout "${logout}" '
      .redirectUris = ((.redirectUris // []) + [$redirect] | unique)
      | .webOrigins = ((.webOrigins // []) + [$origin] | unique)
      | .attributes = (.attributes // {})
      | .attributes["post.logout.redirect.uris"] = (
          (
            (.attributes["post.logout.redirect.uris"] // "")
            | split("##")
            | map(select(length > 0))
          ) + [$logout]
          | unique
          | join("##")
        )
    '
    return
  fi

  if [ "${action}" = "remove" ]; then
    echo "${client_json}" | jq \
      --arg redirect "${redirect}" \
      --arg origin "${origin}" \
      --arg logout "${logout}" '
      .redirectUris = ((.redirectUris // []) | map(select(. != $redirect)))
      | .webOrigins = ((.webOrigins // []) | map(select(. != $origin)))
      | .attributes = (.attributes // {})
      | .attributes["post.logout.redirect.uris"] = (
          (.attributes["post.logout.redirect.uris"] // "")
          | split("##")
          | map(select(length > 0 and . != $logout))
          | join("##")
        )
    '
    return
  fi

  log_error "Unknown Keycloak mutate action: ${action}"
  return 1
}

keycloak_client_has_preview_host() {
  local client_json="$1"
  local web_host="$2"
  local redirect="https://${web_host}/*"

  echo "${client_json}" | jq -e --arg redirect "${redirect}" \
    '(.redirectUris // []) | index($redirect) != null' >/dev/null
}

# Concurrent preview deploys share one client: retry read-modify-write on races.
# Mints a fresh admin token (and refreshes on HTTP 401) so long retry loops do
# not fail mid-update when the access token expires.
keycloak_update_client_preview_host() {
  local action="$1"
  local keycloak_hostname="$2"
  local client_id="$3"
  local web_host="$4"
  local attempt max_attempts=6 token client_uuid current updated delay
  local uuid_rc get_rc put_rc

  token="$(keycloak_admin_token "${keycloak_hostname}")"

  client_uuid=""
  for attempt in $(seq 1 "${max_attempts}"); do
    if [ -z "${client_uuid}" ]; then
      uuid_rc=0
      client_uuid="$(keycloak_client_uuid "${keycloak_hostname}" "${token}" "${client_id}")" || uuid_rc=$?
      if [ "${uuid_rc}" -eq 2 ]; then
        log_info "Keycloak admin token expired looking up ${client_id}; refreshing"
        token="$(keycloak_admin_token "${keycloak_hostname}")"
        client_uuid=""
        continue
      fi
      if [ "${uuid_rc}" -ne 0 ]; then
        return 1
      fi
    fi

    get_rc=0
    current="$(keycloak_get_client "${keycloak_hostname}" "${token}" "${client_uuid}")" || get_rc=$?
    if [ "${get_rc}" -eq 2 ]; then
      log_info "Keycloak admin token expired on GET ${client_id}; refreshing"
      token="$(keycloak_admin_token "${keycloak_hostname}")"
      continue
    fi
    if [ "${get_rc}" -ne 0 ]; then
      return 1
    fi

    if [ "${action}" = "add" ] && keycloak_client_has_preview_host "${current}" "${web_host}"; then
      log_info "Keycloak client ${client_id}: preview host ${web_host} already registered"
      return 0
    fi
    if [ "${action}" = "remove" ] && ! keycloak_client_has_preview_host "${current}" "${web_host}"; then
      log_info "Keycloak client ${client_id}: preview host ${web_host} already absent"
      return 0
    fi

    updated="$(keycloak_mutate_client_preview_host "${action}" "${web_host}" "${current}")"

    put_rc=0
    keycloak_put_client "${keycloak_hostname}" "${token}" "${client_uuid}" "${updated}" || put_rc=$?
    if [ "${put_rc}" -eq 2 ]; then
      log_info "Keycloak admin token expired on PUT ${client_id}; refreshing"
      token="$(keycloak_admin_token "${keycloak_hostname}")"
      continue
    fi
    if [ "${put_rc}" -ne 0 ]; then
      log_info "Keycloak client ${client_id} PUT failed (attempt ${attempt}/${max_attempts}); retrying"
    else
      get_rc=0
      current="$(keycloak_get_client "${keycloak_hostname}" "${token}" "${client_uuid}")" || get_rc=$?
      if [ "${get_rc}" -eq 2 ]; then
        log_info "Keycloak admin token expired verifying ${client_id}; refreshing"
        token="$(keycloak_admin_token "${keycloak_hostname}")"
        continue
      fi
      if [ "${get_rc}" -ne 0 ]; then
        return 1
      fi
      if [ "${action}" = "add" ] && keycloak_client_has_preview_host "${current}" "${web_host}"; then
        log_info "Keycloak client ${client_id}: registered redirect for ${web_host}"
        return 0
      fi
      if [ "${action}" = "remove" ] && ! keycloak_client_has_preview_host "${current}" "${web_host}"; then
        log_info "Keycloak client ${client_id}: removed redirect for ${web_host}"
        return 0
      fi
      log_info "Keycloak client ${client_id} update raced (attempt ${attempt}/${max_attempts}); retrying"
    fi

    delay=$((attempt + RANDOM % 3))
    sleep "${delay}"
  done

  log_error "Gave up updating Keycloak client ${client_id} for ${web_host} after ${max_attempts} attempts"
  return 1
}

ensure_keycloak_preview_host_redirects() {
  local keycloak_hostname="$1"
  local web_host="$2"
  local login_client_id="$3"
  local step_up_client_id="$4"

  require_command curl

  log_info "Registering Keycloak redirect URIs for https://${web_host}"
  # Fresh token per client so step-up registration is not racing token TTL.
  keycloak_update_client_preview_host add \
    "${keycloak_hostname}" "${login_client_id}" "${web_host}"
  keycloak_update_client_preview_host add \
    "${keycloak_hostname}" "${step_up_client_id}" "${web_host}"
}

remove_keycloak_preview_host_redirects() {
  local keycloak_hostname="$1"
  local web_host="$2"
  local login_client_id="$3"
  local step_up_client_id="$4"

  require_command curl

  log_info "Removing Keycloak redirect URIs for https://${web_host}"
  keycloak_update_client_preview_host remove \
    "${keycloak_hostname}" "${login_client_id}" "${web_host}"
  keycloak_update_client_preview_host remove \
    "${keycloak_hostname}" "${step_up_client_id}" "${web_host}"
}
