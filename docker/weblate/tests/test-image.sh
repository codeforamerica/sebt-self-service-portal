#!/bin/bash
# Tests a built Weblate image: the Python suite against the code baked into it, then what
# the container does when Valkey stops answering. Usage: test-image.sh <image>
set -euo pipefail

IMAGE="${1:?usage: test-image.sh <image>}"
TESTS_DIR="$(cd "$(dirname "$0")" && pwd)"
CONTAINER_ID=""

log() { echo "test_image $*"; }

remove_container() {
  if [ -n "$CONTAINER_ID" ]; then
    docker rm -f "$CONTAINER_ID" >/dev/null 2>&1 || true
    CONTAINER_ID=""
  fi
}
trap remove_container EXIT

fail() {
  log "FAIL $1"
  echo "--- container logs ---"
  docker logs "$CONTAINER_ID" 2>&1 | tail -n 50
  exit 1
}

is_running() {
  [ "$(docker inspect -f '{{.State.Running}}' "$CONTAINER_ID")" = "true" ]
}

# Captures the logs before matching: under pipefail, grep -q closing the pipe early fails the pipeline.
logs_contain() {
  local logs
  logs="$(docker logs "$CONTAINER_ID" 2>&1)"
  grep -q "$1" <<<"$logs"
}

run_python_suite() {
  # Weblate's settings read /app/data/secret at import. Its start script writes that file and never runs here.
  docker run --rm --entrypoint /bin/sh -v "$TESTS_DIR:/app/cfa/tests:ro" -w /app/cfa "$IMAGE" -c \
    'umask 077; head -c 48 /dev/urandom | base64 > /app/data/secret; exec /app/venv/bin/python -m unittest discover -s tests -t .'
  log "PASS python suite"
}

start_container_with_valkey() {
  CONTAINER_ID="$(docker run -d \
    -v "$TESTS_DIR/supervisord-only-start:/app/bin/start:ro" \
    -e VALKEY_WATCH_SECONDS=1 -e VALKEY_WATCH_MISSES=2 \
    "$IMAGE")"
  local deadline=$((SECONDS + 30))
  until logs_contain "valkey_ready"; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      fail "Valkey was not ready within 30s"
    fi
    sleep 1
  done
}

# $1 names what happens to Valkey; $2 is the command, run inside the container, that makes it happen.
expect_container_stops_when_valkey() {
  start_container_with_valkey
  sleep 5
  if ! is_running; then
    fail "container stopped while Valkey was healthy"
  fi

  docker exec "$CONTAINER_ID" sh -c "$2"
  local deadline=$((SECONDS + 30))
  while is_running; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      fail "container still running 30s after Valkey $1"
    fi
    sleep 1
  done
  if ! logs_contain "valkey_unresponsive"; then
    fail "container stopped without logging valkey_unresponsive"
  fi
  log "PASS container stops when Valkey $1"
  remove_container
}

run_python_suite
expect_container_stops_when_valkey "exits" "pkill -KILL valkey-server"
expect_container_stops_when_valkey "hangs" "pkill -STOP valkey-server"
