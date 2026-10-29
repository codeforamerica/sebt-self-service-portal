#!/usr/bin/env bash
# Tests for the token requests in scripts/preview/keycloak.sh.
# Stubs `curl` and `aws` on PATH and captures the form body each token request
# sends, so the exact bytes Keycloak would receive can be checked.
set -euo pipefail

TEST_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$TEST_DIR/../../.." && pwd)"
source "$TEST_DIR/_assert.sh"

STUB_DIR="$(mktemp -d)"
trap 'rm -rf "$STUB_DIR"' EXIT

# curl: copies the --data-binary @file body to $STUB_DIR/body, writes a token
# response to the -o file, and prints 200 for -w.
cat > "$STUB_DIR/curl" <<'STUB'
#!/usr/bin/env bash
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift ;;
    --data-binary) cp "${2#@}" "$STUB_DIR/body" ;;
  esac
  shift
done
echo '{"access_token":"fake-token"}' > "$out"
echo 200
STUB
# aws: returns the bootstrap admin secret JSON.
cat > "$STUB_DIR/aws" <<'STUB'
#!/usr/bin/env bash
echo '{"username":"admin","password":"p%ss"}'
STUB
chmod +x "$STUB_DIR/curl" "$STUB_DIR/aws"
export PATH="$STUB_DIR:$PATH" STUB_DIR

source "$PROJECT_ROOT/scripts/preview/common.sh"
source "$PROJECT_ROOT/scripts/preview/keycloak.sh"

# The deploy-client request ends with the client secret, URL-encoded and with
# no trailing newline. Keycloak counts a trailing newline as part of the secret.
PREVIEW_KEYCLOAK_DEPLOY_CLIENT_ID=sebt-preview-deploy \
  PREVIEW_KEYCLOAK_DEPLOY_CLIENT_SECRET='s%cret' \
  keycloak_deploy_token https://keycloak.example >/dev/null
assert_eq "$(cat "$STUB_DIR/body"; echo x)" \
  "grant_type=client_credentials&client_id=sebt-preview-deploy&client_secret=s%25cretx"

# The bootstrap admin request ends with the password, under the same rule.
keycloak_admin_token https://keycloak.example >/dev/null
assert_eq "$(cat "$STUB_DIR/body"; echo x)" \
  "grant_type=password&client_id=admin-cli&username=admin&password=p%25ssx"

echo "preview-keycloak-token_test: OK"
