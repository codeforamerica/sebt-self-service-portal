#!/bin/bash
# Image Smoke Test
# Runs a built image and waits for an HTTP path to return the expected status.
# Fails if the container exits or the status never arrives, and prints the
# container logs when it fails.
#
# Usage:
#   ./scripts/ci/smoke-test-image.sh --image <tag> --port <container-port> --path <path> \
#     [--expect 200] [--env KEY=VALUE]... [--timeout 90]
#
# Options:
#   --image <tag>          Image to run (required).
#   --port <port>          Port the app listens on inside the container (required).
#   --path <path>          HTTP path to request (required).
#   --expect <status>      HTTP status that counts as healthy (default 200).
#   --env KEY=VALUE        Environment variable for the container; repeatable.
#   --timeout <seconds>    How long to wait for the status (default 90).

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_error()   { echo -e "${RED}❌ $1${NC}"; }

IMAGE=""
PORT=""
URL_PATH=""
EXPECT="200"
TIMEOUT="90"
ENV_ARGS=()

while [ $# -gt 0 ]; do
  case "$1" in
    --image) IMAGE="$2"; shift 2 ;;
    --port) PORT="$2"; shift 2 ;;
    --path) URL_PATH="$2"; shift 2 ;;
    --expect) EXPECT="$2"; shift 2 ;;
    --env) ENV_ARGS+=(-e "$2"); shift 2 ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    -h|--help)
      grep '^# ' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) log_error "Unknown argument: $1"; exit 1 ;;
  esac
done

if [ -z "$IMAGE" ] || [ -z "$PORT" ] || [ -z "$URL_PATH" ]; then
  log_error "--image, --port, and --path are required"
  exit 1
fi

# Bind to a random loopback port so parallel runs never collide.
# The ${arr[@]+...} form keeps bash 3.2 (macOS) from treating an empty array as unset.
CONTAINER_ID="$(docker run -d -p "127.0.0.1::$PORT" ${ENV_ARGS[@]+"${ENV_ARGS[@]}"} "$IMAGE")"
trap 'docker rm -f "$CONTAINER_ID" >/dev/null 2>&1 || true' EXIT

HOST_PORT="$(docker port "$CONTAINER_ID" "$PORT" | head -1 | sed 's/.*://')"
URL="http://127.0.0.1:${HOST_PORT}${URL_PATH}"

fail() {
  log_error "$1"
  echo "--- container logs ($IMAGE) ---"
  docker logs "$CONTAINER_ID" 2>&1 | tail -n 200
  exit 1
}

log_info "Waiting up to ${TIMEOUT}s for $IMAGE to return $EXPECT from $URL_PATH"
status="000"
deadline=$((SECONDS + TIMEOUT))
while [ "$SECONDS" -lt "$deadline" ]; do
  if [ "$(docker inspect -f '{{.State.Running}}' "$CONTAINER_ID")" != "true" ]; then
    fail "Container exited before $URL_PATH returned $EXPECT"
  fi
  # curl prints 000 when nothing answers yet; || true keeps that from ending the loop.
  status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL" || true)"
  if [ "$status" = "$EXPECT" ]; then
    log_success "$IMAGE returned $EXPECT from $URL_PATH"
    exit 0
  fi
  sleep 2
done

fail "$URL_PATH did not return $EXPECT within ${TIMEOUT}s (last status: $status)"
