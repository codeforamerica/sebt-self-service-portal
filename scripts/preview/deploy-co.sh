#!/usr/bin/env bash
# Deploy a CO preview stack (API + Web) into the existing dev-co ECS cluster.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

usage() {
  cat <<'EOF'
Usage: deploy-co.sh <pr-number> <image-tag>

Environment:
  PREVIEW_DOMAIN / DOMAIN          Public preview domain (required)
  PREVIEW_HOSTED_ZONE_ID           Route53 hosted zone ID (optional; auto-discovered)
  PREVIEW_ECS_CLUSTER              Shared cluster override (optional; legacy)
  PREVIEW_API_ECS_CLUSTER          API ECS cluster name (optional; auto-discovered)
  PREVIEW_WEB_ECS_CLUSTER          Web ECS cluster name (optional; auto-discovered)
  PREVIEW_BASE_API_SERVICE         Base dev-co API ECS service name (optional)
  PREVIEW_BASE_WEB_SERVICE         Base dev-co Web ECS service name (optional)
  PREVIEW_API_SERVICE              Alias for PREVIEW_BASE_API_SERVICE (optional)
  PREVIEW_WEB_SERVICE              Alias for PREVIEW_BASE_WEB_SERVICE (optional)
  PREVIEW_INGRESS_CIDR             CIDR allowed for direct HTTPS to web ALB (default: 0.0.0.0/0)
  ECR_API_REPOSITORY_URL           API image repository (required)
  ECR_WEB_REPOSITORY_URL           Web image repository (required)
  AWS_REGION                       AWS region (default: us-east-1)
EOF
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
  usage
  exit 0
fi

PR_NUMBER="${1:-}"
IMAGE_TAG="${2:-}"

if [ -z "${PR_NUMBER}" ] || [ -z "${IMAGE_TAG}" ]; then
  usage
  exit 1
fi

if [ -z "${ECR_API_REPOSITORY_URL:-}" ] || [ -z "${ECR_WEB_REPOSITORY_URL:-}" ]; then
  log_error "ECR_API_REPOSITORY_URL and ECR_WEB_REPOSITORY_URL must be set"
  exit 1
fi

preview_requirements

export AWS_REGION="${AWS_REGION:-us-east-1}"
export PR_NUMBER

DOMAIN="$(resolve_preview_domain)"
HOSTED_ZONE_ID="$(resolve_hosted_zone_id "${DOMAIN}")"
API_CLUSTER="$(discover_cluster_for_role api)"
WEB_CLUSTER="$(discover_cluster_for_role web)"
BASE_API_SERVICE="$(discover_base_service api "${API_CLUSTER}")"
BASE_WEB_SERVICE="$(discover_base_service web "${WEB_CLUSTER}")"

API_HOST="api-pr-${PR_NUMBER}.${DOMAIN}"
WEB_HOST="pr-${PR_NUMBER}.${DOMAIN}"
STACK_API_SERVICE="sebt-co-preview-pr-${PR_NUMBER}-api"
STACK_WEB_SERVICE="sebt-co-preview-pr-${PR_NUMBER}-web"
API_TASK_FAMILY="sebt-co-preview-pr-${PR_NUMBER}-api"
WEB_TASK_FAMILY="sebt-co-preview-pr-${PR_NUMBER}-web"
API_TG_NAME="co-pr-${PR_NUMBER}-api"
WEB_TG_NAME="co-pr-${PR_NUMBER}-web"
API_LISTENER_PRIORITY="$(preview_listener_priority "${PR_NUMBER}" 0)"
WEB_LISTENER_PRIORITY="$(preview_listener_priority "${PR_NUMBER}" 1)"

API_IMAGE="${ECR_API_REPOSITORY_URL}:${IMAGE_TAG}"
WEB_IMAGE="${ECR_WEB_REPOSITORY_URL}:${IMAGE_TAG}"

log_info "Deploying preview for PR ${PR_NUMBER} (api_cluster=${API_CLUSTER}, web_cluster=${WEB_CLUSTER}, tag=${IMAGE_TAG})"

API_BASE_TD="$(get_service_task_definition "${API_CLUSTER}" "${BASE_API_SERVICE}")"
WEB_BASE_TD="$(get_service_task_definition "${WEB_CLUSTER}" "${BASE_WEB_SERVICE}")"

API_LB="$(get_service_load_balancer "${API_CLUSTER}" "${BASE_API_SERVICE}")"
WEB_LB="$(get_service_load_balancer "${WEB_CLUSTER}" "${BASE_WEB_SERVICE}")"

API_CONTAINER_NAME="$(echo "${API_LB}" | jq -r '.containerName')"
WEB_CONTAINER_NAME="$(echo "${WEB_LB}" | jq -r '.containerName')"
API_CONTAINER_PORT="$(echo "${API_LB}" | jq -r '.containerPort')"
WEB_CONTAINER_PORT="$(echo "${WEB_LB}" | jq -r '.containerPort')"

API_BASE_TG="$(echo "${API_LB}" | jq -r '.targetGroupArn')"
WEB_BASE_TG="$(echo "${WEB_LB}" | jq -r '.targetGroupArn')"

API_ALB_ARN="$(get_target_group_lb_arn "${API_BASE_TG}")"
WEB_ALB_ARN="$(get_target_group_lb_arn "${WEB_BASE_TG}")"

API_LISTENER_ARN="$(get_https_listener_arn "${API_ALB_ARN}")"
WEB_LISTENER_ARN="$(get_https_listener_arn "${WEB_ALB_ARN}")"

API_VPC_ID="$(get_alb_vpc_id "${API_ALB_ARN}")"
WEB_VPC_ID="$(get_alb_vpc_id "${WEB_ALB_ARN}")"

EXISTING_API_TG="$(aws elbv2 describe-target-groups --names "${API_TG_NAME}" \
  --query 'TargetGroups[0].TargetGroupArn' --output text 2>/dev/null || echo "")"
if [ "${EXISTING_API_TG}" = "None" ]; then
  EXISTING_API_TG=""
fi

EXISTING_WEB_TG="$(aws elbv2 describe-target-groups --names "${WEB_TG_NAME}" \
  --query 'TargetGroups[0].TargetGroupArn' --output text 2>/dev/null || echo "")"
if [ "${EXISTING_WEB_TG}" = "None" ]; then
  EXISTING_WEB_TG=""
fi

API_TG_ARN="$(ensure_target_group "${API_TG_NAME}" "${API_CONTAINER_PORT}" "/health" "${API_VPC_ID}" "${EXISTING_API_TG}")"
WEB_TG_ARN="$(ensure_target_group "${WEB_TG_NAME}" "${WEB_CONTAINER_PORT}" "/" "${WEB_VPC_ID}" "${EXISTING_WEB_TG}")"

EXISTING_API_RULE="$(find_listener_rule_for_host "${API_LISTENER_ARN}" "${API_HOST}")"
EXISTING_WEB_RULE="$(find_listener_rule_for_host "${WEB_LISTENER_ARN}" "${WEB_HOST}")"

ensure_listener_rule "${API_LISTENER_ARN}" "${API_LISTENER_PRIORITY}" "${API_HOST}" "${API_TG_ARN}" "${EXISTING_API_RULE}" >/dev/null
ensure_listener_rule "${WEB_LISTENER_ARN}" "${WEB_LISTENER_PRIORITY}" "${WEB_HOST}" "${WEB_TG_ARN}" "${EXISTING_WEB_RULE}" >/dev/null

API_ENV_OVERRIDES="$(jq -n '{
  "ASPNETCORE_ENVIRONMENT": "Staging",
  "STATE": "co",
  "UseMockHouseholdData": "true",
  "Cbms__UseMockResponses": "true",
  "Seeding__Enabled": "true",
  "Seeding__EmailPattern": "sebt.co+{0}@codeforamerica.org",
  "Seeding__State": "co",
  "FeatureManagement__bypass_otp": "true",
  "PluginAssemblyPaths__0": "plugins-co",
  "Socure__Enabled": "false",
  "Smarty__Enabled": "false"
}')"

WEB_ENV_OVERRIDES="$(jq -n \
  --arg backend_url "https://${API_HOST}" \
  '{
    "STATE": "co",
    "NEXT_PUBLIC_STATE": "co",
    "BACKEND_URL": $backend_url
  }')"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

API_TD_ARN="$(register_preview_task_definition \
  "${API_BASE_TD}" "${API_IMAGE}" "${API_TASK_FAMILY}" "${API_ENV_OVERRIDES}" \
  "${TMP_DIR}/api-task.json" "${API_CONTAINER_NAME}")"

WEB_TD_ARN="$(register_preview_task_definition \
  "${WEB_BASE_TD}" "${WEB_IMAGE}" "${WEB_TASK_FAMILY}" "${WEB_ENV_OVERRIDES}" \
  "${TMP_DIR}/web-task.json" "${WEB_CONTAINER_NAME}")"

ensure_ecs_service \
  "${API_CLUSTER}" "${STACK_API_SERVICE}" "${API_TD_ARN}" "${API_TG_ARN}" \
  "${API_CONTAINER_NAME}" "${API_CONTAINER_PORT}" "${BASE_API_SERVICE}" \
  "${TMP_DIR}/api-network.json"

ensure_ecs_service \
  "${WEB_CLUSTER}" "${STACK_WEB_SERVICE}" "${WEB_TD_ARN}" "${WEB_TG_ARN}" \
  "${WEB_CONTAINER_NAME}" "${WEB_CONTAINER_PORT}" "${BASE_WEB_SERVICE}" \
  "${TMP_DIR}/web-network.json"

wait_for_preview_services_stable \
  "${API_CLUSTER}" "${WEB_CLUSTER}" "${STACK_API_SERVICE}" "${STACK_WEB_SERVICE}"

ensure_route53_alias \
  "${HOSTED_ZONE_ID}" \
  "${API_HOST}" \
  "$(get_alb_dns_name "${API_ALB_ARN}")" \
  "$(get_alb_hosted_zone_id "${API_ALB_ARN}")"

WEB_ALB_DNS="$(get_alb_dns_name "${WEB_ALB_ARN}")"
WEB_ALB_ZONE="$(get_alb_hosted_zone_id "${WEB_ALB_ARN}")"
ensure_route53_alias "${HOSTED_ZONE_ID}" "${WEB_HOST}" "${WEB_ALB_DNS}" "${WEB_ALB_ZONE}"

# Dev web ALBs are often reachable only through CloudFront. Allow direct HTTPS for preview hosts.
while IFS= read -r security_group_id; do
  [ -n "${security_group_id}" ] || continue
  ensure_preview_https_ingress "${security_group_id}"
done < <(get_alb_security_groups "${WEB_ALB_ARN}" | jq -r '.[]')

# Record deploy marker so PR-close destroy can skip PRs that never deployed a preview.
write_preview_deploy_marker "${IMAGE_TAG}"

PREVIEW_URL="https://${WEB_HOST}"
log_info "Preview URL: ${PREVIEW_URL}"
echo "preview_url=${PREVIEW_URL}"
