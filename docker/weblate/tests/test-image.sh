#!/bin/bash
# Tests a built Weblate image: the Python suite against the code baked into it, then Valkey's memory
# cap and what the container does when Valkey stops answering. Usage: test-image.sh <image>
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

# Extra arguments go to docker run, ahead of the image.
start_container_with_valkey() {
  CONTAINER_ID="$(docker run -d \
    -v "$TESTS_DIR/supervisord-only-start:/app/bin/start:ro" \
    -e VALKEY_WATCH_SECONDS=1 -e VALKEY_WATCH_MISSES=2 \
    "$@" "$IMAGE")"
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

valkey_cli() {
  docker exec "$CONTAINER_ID" valkey-cli "$@" | tr -d '\r'
}

expect_valkey_memory_cap_by_default() {
  start_container_with_valkey
  local cap_bytes
  cap_bytes="$(valkey_cli config get maxmemory | tail -n 1)"
  if [ "$cap_bytes" = "0" ]; then
    fail "Valkey started with no memory cap"
  fi
  log "PASS Valkey starts with a memory cap cap_bytes=$cap_bytes"
  remove_container
}

# Celery's queues carry no expiry and cache entries do, so a full Valkey has to make room from the cache alone.
expect_full_valkey_evicts_only_expiring_keys() {
  start_container_with_valkey -e VALKEY_MAXMEMORY=4mb
  valkey_cli rpush queue-without-expiry task >/dev/null

  local replies
  replies="$(docker exec "$CONTAINER_ID" sh -c '
    payload="$(head -c 10000 /dev/zero | tr "\0" x)"
    for i in $(seq 1 2000); do echo "SET cache:$i $payload EX 3600"; done | valkey-cli')"
  if grep -qv '^OK' <<<"$replies"; then
    fail "Valkey refused a write at the cap: $(grep -v '^OK' <<<"$replies" | head -n 1)"
  fi

  local evicted
  evicted="$(valkey_cli info stats | grep '^evicted_keys:' | cut -d: -f2)"
  if [ "$evicted" -eq 0 ]; then
    fail "Valkey took 20 MB of expiring keys under a 4 MB cap without evicting any"
  fi
  if [ "$(valkey_cli llen queue-without-expiry)" != "1" ]; then
    fail "Valkey evicted a key that has no expiry"
  fi
  log "PASS full Valkey evicts only expiring keys evicted=$evicted"
  remove_container
}

run_python_suite
expect_valkey_memory_cap_by_default
expect_full_valkey_evicts_only_expiring_keys
expect_container_stops_when_valkey "exits" "pkill -KILL valkey-server"
expect_container_stops_when_valkey "hangs" "pkill -STOP valkey-server"
