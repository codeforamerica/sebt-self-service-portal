#!/usr/bin/env bash
# Tests for scripts/preview/sync-keycloak-fixture-users.sh.
# Pure jq transforms run against the real realm file; the create-vs-update
# decision runs against a stubbed keycloak_admin_request that records calls.
set -euo pipefail

TEST_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$TEST_DIR/../../.." && pwd)"
source "$TEST_DIR/_assert.sh"

REALM_FILE="$PROJECT_ROOT/docker/keycloak/sebt-realm.preview.json"
CALLS="$(mktemp)"
trap 'rm -f "$CALLS"' EXIT

source "$PROJECT_ROOT/scripts/preview/sync-keycloak-fixture-users.sh"

# Extraction: service account excluded, user without phone attribute included.
usernames="$(fixture_users "$REALM_FILE" | jq -r '.username')"
if grep -q '^service-account-' <<<"$usernames"; then
  echo "ASSERT FAIL: service account was extracted" >&2
  exit 1
fi
assert_eq "$(grep -cx 'minimal' <<<"$usernames")" "1"
assert_eq "$(grep -cx 'verified' <<<"$usernames")" "1"

minimal="$(fixture_users "$REALM_FILE" | jq -c 'select(.username == "minimal")')"

# Representation drops credentials but keeps profile fields.
rep="$(user_without_credentials <<<"$minimal")"
assert_eq "$(jq 'has("credentials")' <<<"$rep")" "false"
assert_eq "$(jq -r '.email' <<<"$rep")" "sebt.co+minimal@codeforamerica.org"
assert_eq "$(jq -c '.attributes' <<<"$rep")" "{}"

# Password credential is picked as-is.
assert_eq "$(password_credential <<<"$minimal")" \
  '{"type":"password","value":"password","temporary":false}'

# Stub: records "METHOD URL [payload]"; lookup answers from EXISTING_USER.
keycloak_admin_request() {
  local method="$1" url="$2" outfile="$4" payload_file="${5:-}"
  echo "$method $url ${payload_file:+$(cat "$payload_file")}" >>"$CALLS"
  if [ "$method" = "GET" ] && [ -n "${EXISTING_USER:-}" ]; then
    echo "[{\"id\":\"abc-123\",\"username\":\"${EXISTING_USER}\"}]" >"$outfile"
  else
    echo '[]' >"$outfile"
  fi
  if [ "$method" = "POST" ]; then echo 201; else echo 200; fi
}

H=https://kc.example
BASE="$H/admin/realms/sebt/users"

# Missing user: lookup, then a POST with credentials; no PUT.
: >"$CALLS"
EXISTING_USER="" sync_user "$H" tok "$minimal" false
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "2"
assert_contains "$CALLS" "GET $BASE?username=minimal&exact=true"
assert_contains "$CALLS" "POST $BASE {"
assert_contains "$CALLS" '"credentials"'

# Existing user: lookup, PUT without credentials, then reset-password; no POST.
: >"$CALLS"
EXISTING_USER=minimal sync_user "$H" tok "$minimal" false
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "3"
if grep -q '^POST' "$CALLS"; then echo "ASSERT FAIL: unexpected POST" >&2; exit 1; fi
assert_contains "$CALLS" "PUT $BASE/abc-123 {"
assert_contains "$CALLS" "PUT $BASE/abc-123/reset-password {\"type\":\"password\""
if grep "^PUT $BASE/abc-123 " "$CALLS" | grep -q credentials; then
  echo "ASSERT FAIL: PUT body carried credentials" >&2
  exit 1
fi

# Dry run: lookup only, still reports the planned action.
: >"$CALLS"
out="$(EXISTING_USER=minimal sync_user "$H" tok "$minimal" true 2>&1)"
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "1"
assert_contains <(echo "$out") "minimal: would update"

# User profile: a realm with no unmanaged policy gets ADMIN_EDIT; one that allows admins is left alone;
# dry run only reads.
PROFILE_URL="$H/admin/realms/sebt/users/profile"
keycloak_admin_request() {
  local method="$1" url="$2" outfile="$4" payload_file="${5:-}"
  echo "$method $url ${payload_file:+$(cat "$payload_file")}" >>"$CALLS"
  echo "${PROFILE_JSON}" >"$outfile"
  echo 200
}

: >"$CALLS"
PROFILE_JSON='{"attributes":[{"name":"username"}]}' ensure_admin_can_edit_unmanaged_attributes "$H" tok false
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "2"
assert_contains "$CALLS" "PUT $PROFILE_URL "
assert_contains "$CALLS" '"unmanagedAttributePolicy":"ADMIN_EDIT"'
assert_contains "$CALLS" '"attributes":[{"name":"username"}]'

: >"$CALLS"
PROFILE_JSON='{"unmanagedAttributePolicy":"ADMIN_EDIT"}' ensure_admin_can_edit_unmanaged_attributes "$H" tok false
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "1"

: >"$CALLS"
out="$(PROFILE_JSON='{"unmanagedAttributePolicy":"ADMIN_VIEW"}' ensure_admin_can_edit_unmanaged_attributes "$H" tok true 2>&1)"
assert_eq "$(wc -l <"$CALLS" | tr -d ' ')" "1"
assert_contains <(echo "$out") "would set unmanagedAttributePolicy to ADMIN_EDIT (now ADMIN_VIEW)"

echo "sync-keycloak-fixture-users_test: OK"
