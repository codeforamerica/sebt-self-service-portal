#!/usr/bin/env bash
# All States Local Run
#
# Runs every state's portal side by side, so a change can be checked against DC and CO at once
# without switching STATE. Each state gets its own API, web server, database and build output.
#
# Usage:
#   ./scripts/dev/run-all-states.sh [--dry-run] [--root DIR]
#
#   --dry-run   Print each state's commands without building or starting anything.
#   --root DIR  Use another checkout (the smoke test uses it).
#
# Expects local setup to be done (./scripts/dev/setup-local.sh) and MSSQL running.
#
#   State  Web                        API
#   CO     http://localhost:3000      http://localhost:5280
#   DC     http://dc.localhost:3002   http://localhost:5281
#
# Why each state is isolated the way it is:
#   - APIs run under dotnet watch, each with its own --artifacts-path, so both hot reload without
#     overwriting each other's build and restore output.
#   - Web servers run next dev with their own NEXT_DIST_DIR: Next refuses to start a second dev
#     server on a build directory that is already in use.
#   - Databases SebtPortal_co and SebtPortal_dc are created, migrated and seeded on first start, so
#     the two APIs never race on migrations or share seeded users.
#   - DC is served from dc.localhost because browsers scope cookies by hostname, not port; the two
#     states' session cookies would otherwise overwrite each other. CO stays on localhost:3000,
#     where its OIDC callback is registered.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DRY_RUN=false

usage_error() {
  echo "Usage: $0 [--dry-run] [--root DIR]" >&2
  exit 2
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --root)
      [ -n "${2:-}" ] || usage_error
      ROOT="$2"
      shift 2
      ;;
    *) usage_error ;;
  esac
done

# state:api-port:web-port:web-host
STATES="co:5280:3000:localhost dc:5281:3002:dc.localhost"
API_PROJECT="apps/portal/src/SEBT.Portal.Api"
WEB_DIR="apps/portal/src/SEBT.Portal.Web"

# Reads one value from .env, then from .env.example, which carries the defaults compose applies.
env_value() {
  local value
  value="$(grep "^$1=" "$ROOT/.env" 2> /dev/null | cut -d= -f2- || true)"
  [ -n "$value" ] || value="$(grep "^$1=" "$ROOT/.env.example" 2> /dev/null | cut -d= -f2- || true)"
  echo "${value:-${2:-}}"
}

db_port="$(env_value MSSQL_PORT 1433)"
db_user="$(env_value MSSQL_USER sa)"
db_password="$(env_value MSSQL_SA_PASSWORD)"
# The dry run shows commands on screen, so it never prints the real password.
if [ "$DRY_RUN" = true ]; then
  db_password='***'
fi

# Wraps a value in single quotes for the shell that runs each command.
quote() {
  printf "'%s'" "${1//\'/\'\\\'\'}"
}

labels=()
commands=()
ports=""
urls=""
for entry in $STATES; do
  IFS=: read -r state api_port web_port web_host <<< "$entry"
  connection="Server=localhost,$db_port;Database=SebtPortal_$state;User Id=$db_user;Password=$db_password;TrustServerCertificate=True;"

  labels+=("$state:api" "$state:web")
  commands+=(
    "STATE=$state ASPNETCORE_ENVIRONMENT=Development Seeding__EnableDevEndpoints=true ASPNETCORE_URLS=http://localhost:$api_port ConnectionStrings__DefaultConnection=$(quote "$connection") dotnet watch --project $API_PROJECT --non-interactive run --no-launch-profile --artifacts-path artifacts/all-states/$state"
    "STATE=$state BACKEND_URL=http://localhost:$api_port NEXT_DIST_DIR=.next-$state pnpm --filter @sebt/web exec next dev --turbopack --port $web_port"
  )
  ports="$ports $api_port $web_port"
  urls="$urls  ${state}: http://$web_host:$web_port\n"
done

if [ "$DRY_RUN" = true ]; then
  for i in "${!commands[@]}"; do
    echo "${labels[$i]} ${commands[$i]}"
  done
  echo -e "\nPortals:\n$urls"
  exit 0
fi

if [ "$(docker inspect -f '{{.State.Health.Status}}' sebt_mssql 2> /dev/null || true)" != "healthy" ]; then
  echo "❌ MSSQL is not running. Start it with: docker compose up -d --wait mssql" >&2
  exit 1
fi

# Built once up front: this also stages both states' plugin folders, which the per-state watch
# builds do not. The web servers share generated tokens, themes and content, so those are generated
# once rather than by two servers at the same moment.
"$SCRIPT_DIR/build-dc.sh"
(cd "$ROOT/$WEB_DIR" && pnpm --silent run predev)

# A busy port stops the run instead of being freed: whatever holds it may be unrelated to this repo.
busy=""
for port in $ports; do
  holder="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN 2> /dev/null | awk 'NR == 2 {print $1 " (pid " $2 ")"}')"
  if [ -n "$holder" ]; then
    busy="$busy  port $port is in use by $holder\n"
  fi
done
if [ -n "$busy" ]; then
  echo -e "❌ Stop these before running every state:\n$busy" >&2
  exit 1
fi

echo -e "\nPortals:\n$urls"
cd "$ROOT"
exec pnpm exec concurrently --names "$(IFS=,; echo "${labels[*]}")" --prefix-colors "blue,green,magenta,cyan" "${commands[@]}"
