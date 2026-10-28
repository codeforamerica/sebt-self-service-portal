#!/usr/bin/env bash
# Tests for scripts/ci/smoke-test-image.sh.
# Stubs `docker` and `curl` on PATH so the script's decisions can be checked
# without a Docker daemon or a real image.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

SMOKE="$PROJECT_ROOT/scripts/ci/smoke-test-image.sh"

STUB_DIR="$(mktemp -d)"
trap 'rm -rf "$STUB_DIR"' EXIT

# FAKE_RUNNING: what `docker inspect` reports for State.Running.
# FAKE_STATUS: the HTTP status `curl` prints.
# Every docker call is appended to $STUB_DIR/docker.calls.
cat > "$STUB_DIR/docker" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$STUB_DIR/docker.calls"
case "$1" in
  run) echo "fake-container-id" ;;
  port) echo "127.0.0.1:49153" ;;
  inspect) echo "${FAKE_RUNNING:-true}" ;;
  logs) echo "fake container logs" ;;
esac
STUB
cat > "$STUB_DIR/curl" <<'STUB'
#!/usr/bin/env bash
echo "${FAKE_STATUS:-200}"
STUB
chmod +x "$STUB_DIR/docker" "$STUB_DIR/curl"
export PATH="$STUB_DIR:$PATH" STUB_DIR

run_smoke() {
  : > "$STUB_DIR/docker.calls"
  set +e
  OUTPUT="$(bash "$SMOKE" "$@" 2>&1)"
  STATUS=$?
  set -e
}

# Passes when the expected status comes back, and forwards --env to docker run.
FAKE_STATUS=200 run_smoke --image web:test --port 3000 --path / --expect 200 --env STATE=co --timeout 2
assert_eq "$STATUS" "0"
assert_contains "$STUB_DIR/docker.calls" "-e STATE=co"
assert_contains "$STUB_DIR/docker.calls" "web:test"
assert_contains "$STUB_DIR/docker.calls" "rm -f fake-container-id"

# --expect defaults to 200.
FAKE_STATUS=200 run_smoke --image api:test --port 8080 --path /health --timeout 2
assert_eq "$STATUS" "0"
FAKE_STATUS=503 run_smoke --image api:test --port 8080 --path /health --timeout 2
assert_eq "$STATUS" "1"

# Fails, and prints the container logs, when the container exits.
FAKE_RUNNING=false run_smoke --image api:test --port 8080 --path /health --timeout 2
assert_eq "$STATUS" "1"
grep -qF "fake container logs" <<<"$OUTPUT" || { echo "ASSERT FAIL: expected container logs in output" >&2; exit 1; }

# Fails on timeout when the expected status never arrives.
FAKE_STATUS=500 run_smoke --image web:test --port 3000 --path / --expect 200 --timeout 2
assert_eq "$STATUS" "1"
grep -qF "last status: 500" <<<"$OUTPUT" || { echo "ASSERT FAIL: expected last status in output" >&2; exit 1; }

# Fails when no response arrives at all (curl prints 000).
FAKE_STATUS=000 run_smoke --image web:test --port 3000 --path / --timeout 2
assert_eq "$STATUS" "1"

# Rejects a missing --image.
run_smoke --port 3000 --path /
assert_eq "$STATUS" "1"

echo "smoke-test-image_test: OK"
