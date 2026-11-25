#!/usr/bin/env bash
# Turn the demo environment on or off, or reset its data, by scaling its ECS services.
# Usage: scripts/demo/power.sh <on|off|reset>
#   on     desired count 1 on the API and web services
#   off    desired count 0 on the API and web services
#   reset  force a new API deployment (the API clears and reseeds demo data on start)
# Always targets the demo services; the names are fixed so an exported STATE=co or
# STATE=dc cannot redirect it. AWS_REGION and AWS_PROFILE are honored.

set -euo pipefail

readonly PROJECT="sebt-portal"
readonly STATE="demo"
readonly ENVIRONMENT="development"
export AWS_REGION="${AWS_REGION:-us-east-1}"

usage() {
  echo "usage: power.sh <on|off|reset>" >&2
  exit 1
}

[[ $# -eq 1 ]] || usage
ACTION="$1"
case "$ACTION" in
  on|off|reset) ;;
  *) usage ;;
esac

PREFIX="${PROJECT}-${STATE}-${ENVIRONMENT}"
API="${PREFIX}-api"
WEB="${PREFIX}-web"

# The cluster and service share a name for each of the API and web.
for name in "$API" "$WEB"; do
  status=$(aws ecs describe-clusters --clusters "$name" \
    --query 'clusters[0].status' --output text 2>/dev/null || true)
  if [[ "$status" != "ACTIVE" ]]; then
    echo "error: ECS cluster '${name}' is not ACTIVE in ${AWS_REGION} (status: ${status:-none})" >&2
    exit 1
  fi
done

set_desired_count() {
  local name="$1" count="$2"
  aws ecs update-service --cluster "$name" --service "$name" \
    --desired-count "$count" >/dev/null
  echo "${name}: desired count ${count}"
}

case "$ACTION" in
  on)
    set_desired_count "$API" 1
    set_desired_count "$WEB" 1
    ;;
  off)
    set_desired_count "$API" 0
    set_desired_count "$WEB" 0
    ;;
  reset)
    aws ecs update-service --cluster "$API" --service "$API" \
      --force-new-deployment >/dev/null
    echo "${API}: new deployment started"
    ;;
esac
