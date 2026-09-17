#!/usr/bin/env bash
# Local Environment Setup
#
# One command from a fresh clone to a machine that can run the portal: installs missing tools,
# fetches the DC connector, writes local config, installs dependencies, builds, and starts the Docker
# services. Every step checks before it acts, so re-running is safe.
#
# Usage:
#   ./scripts/dev/setup-local.sh [--state dc|co]
#
# Without --state, sets up both states. DC also needs the sibling sebt-self-service-portal-dc-connector
# repository; when it cannot be cloned (for example, no access), setup warns and continues for CO only.
#
# macOS with Homebrew only. On other platforms, follow the manual steps in README.md.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DC_CONNECTOR_DIR="$(cd "$PROJECT_ROOT/.." && pwd)/sebt-self-service-portal-dc-connector"
DC_CONNECTOR_REPO="https://github.com/codeforamerica/sebt-self-service-portal-dc-connector.git"
TEAM_PNPM_MAJOR=10
MIN_NODE_MAJOR=24

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_step() { echo -e "\n${BLUE}▶ $1${NC}"; }
log_info() { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
fail() {
  echo -e "${RED}❌ $1${NC}" >&2
  exit 1
}

usage() {
  sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'
}

STATES="dc co"
while [ "$#" -gt 0 ]; do
  case "$1" in
    --state)
      case "${2:-}" in
        dc | co) STATES="$2" ;;
        *) fail "Unknown state: ${2:-} (expected dc or co)" ;;
      esac
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      usage >&2
      exit 2
      ;;
  esac
done

includes_state() {
  case " $STATES " in
    *" $1 "*) return 0 ;;
    *) return 1 ;;
  esac
}

major_version() {
  "$1" --version 2> /dev/null | sed -E 's/^v//; s/\..*//' || true
}

check_platform() {
  log_step "Checking platform"
  [ "$(uname -s)" = "Darwin" ] || fail "This script supports macOS only. Follow the manual steps in README.md."
  command -v brew > /dev/null || fail "Homebrew is required. Install it from https://brew.sh, then re-run."
  command -v git > /dev/null || fail "git is required. Run: xcode-select --install"
  log_success "macOS with Homebrew"
}

install_prerequisites() {
  log_step "Installing missing tools"

  if ! dotnet --list-sdks 2> /dev/null | grep -q '^10\.'; then
    log_info "Installing the .NET 10 SDK"
    brew install dotnet
  fi
  dotnet --list-sdks 2> /dev/null | grep -q '^10\.' \
    || fail "No .NET 10 SDK after brew install dotnet. Run: brew upgrade dotnet, then re-run."
  log_success ".NET $(dotnet --version)"

  local node_major
  node_major="$(major_version node)"
  if [ -z "$node_major" ] || [ "$node_major" -lt "$MIN_NODE_MAJOR" ]; then
    log_info "Installing Node.js $MIN_NODE_MAJOR or newer"
    brew install node || brew upgrade node
    node_major="$(major_version node)"
    [ -n "$node_major" ] && [ "$node_major" -ge "$MIN_NODE_MAJOR" ] \
      || fail "Node.js $MIN_NODE_MAJOR or newer is still not first on PATH. Check for nvm or other Node installs, then re-run."
  fi
  log_success "Node.js $(node --version)"

  local pnpm_major
  pnpm_major="$(major_version pnpm)"
  if [ -z "$pnpm_major" ] || [ "$pnpm_major" -lt "$TEAM_PNPM_MAJOR" ]; then
    log_info "Installing pnpm $TEAM_PNPM_MAJOR"
    npm install -g "pnpm@$TEAM_PNPM_MAJOR"
    pnpm_major="$(major_version pnpm)"
    [ "$pnpm_major" = "$TEAM_PNPM_MAJOR" ] \
      || fail "pnpm $TEAM_PNPM_MAJOR is still not first on PATH. Check for other pnpm installs, then re-run."
  elif [ "$pnpm_major" -gt "$TEAM_PNPM_MAJOR" ]; then
    log_warning "pnpm $(pnpm --version) found; the team uses pnpm $TEAM_PNPM_MAJOR. Continuing."
  fi
  log_success "pnpm $(pnpm --version)"

  # Docker Desktop is left to the developer: installing it needs admin rights, and it has to be
  # opened once from the GUI before its daemon runs.
  command -v docker > /dev/null \
    || fail "Docker Desktop is required. Run: brew install --cask docker, open Docker Desktop, then re-run."
  docker info > /dev/null 2>&1 \
    || fail "Docker Desktop is not running. Open it, wait until it reports running, then re-run."
  log_success "Docker is running"
}

# An existing checkout is the developer's own, so it is never switched or pulled. The DC plugin
# builds against this repository's connector contract, though, so an old branch can fail to build
# when shared package versions have moved on; say so before the build does.
warn_if_dc_connector_is_stale() {
  local branch behind
  branch="$(git -C "$DC_CONNECTOR_DIR" branch --show-current)"
  GIT_TERMINAL_PROMPT=0 git -C "$DC_CONNECTOR_DIR" fetch --quiet origin main 2> /dev/null || return 0
  behind="$(git -C "$DC_CONNECTOR_DIR" rev-list --count HEAD..origin/main)"
  if [ "$branch" != "main" ] || [ "$behind" -gt 0 ]; then
    log_warning "The DC connector is on '$branch', $behind commit(s) behind origin/main. If the DC build fails, update that checkout."
  fi
}

setup_dc_connector() {
  includes_state dc || return 0
  log_step "Setting up the DC connector"

  if [ -d "$DC_CONNECTOR_DIR/.git" ]; then
    log_success "Found $DC_CONNECTOR_DIR"
    warn_if_dc_connector_is_stale
    return 0
  fi

  # No credential prompt: without access the clone fails fast instead of waiting on input.
  if GIT_TERMINAL_PROMPT=0 git clone "$DC_CONNECTOR_REPO" "$DC_CONNECTOR_DIR"; then
    log_success "Cloned the DC connector"
    return 0
  fi

  local manual="Clone it beside this repository as described in README.md (To run the DC Portal)."
  [ "$STATES" != "dc" ] || fail "Could not clone the DC connector. $manual"
  log_warning "Could not clone the DC connector, continuing with CO only. $manual"
  STATES="co"
}

write_config() {
  log_step "Writing local config"
  local state_args=""
  for state in $STATES; do
    state_args="$state_args --state $state"
  done
  # shellcheck disable=SC2086 # state_args is a list of flags
  CONFIG_OUTPUT="$("$SCRIPT_DIR/setup-local-config.sh" $state_args)"
  echo "$CONFIG_OUTPUT"
}

install_dependencies() {
  log_step "Installing dependencies"
  (cd "$PROJECT_ROOT" && pnpm install && dotnet tool restore)
}

build() {
  log_step "Building"
  # The DC build also builds the whole monorepo, CO plugin included.
  if includes_state dc; then
    "$SCRIPT_DIR/build-dc.sh"
  else
    "$SCRIPT_DIR/build-co.sh"
  fi
}

start_services() {
  log_step "Starting Docker services"
  (cd "$PROJECT_ROOT" \
    && "$SCRIPT_DIR/gen-redis-certs.sh" \
    && docker compose up -d --wait --wait-timeout 180 mssql mailpit redis)
  log_success "MSSQL, Mailpit and Redis are running"
}

print_summary() {
  echo ""
  log_success "Local environment is ready."
  echo ""
  echo "Start the app:"
  for state in $STATES; do
    echo "  pnpm dev:$state"
  done
  echo ""
  echo "Then open http://localhost:3000"
  if includes_state dc; then
    echo "DC sign-in codes arrive in Mailpit: http://localhost:8025"
  fi
  if includes_state co; then
    echo "CO signs in through OIDC; for a local identity provider see docs/development/keycloak-oidc.md"
  fi

  # The config step prints its placeholder list last, so everything from that heading on is the list.
  local placeholders
  placeholders="$(echo "$CONFIG_OUTPUT" | sed -n '/Placeholders still to fill in/,$p')"
  if [ -n "$placeholders" ]; then
    echo ""
    echo "$placeholders"
  fi
}

check_platform
install_prerequisites
setup_dc_connector
# Config comes before the Docker services so compose reads the database password from the new .env.
write_config
install_dependencies
build
start_services
print_summary
