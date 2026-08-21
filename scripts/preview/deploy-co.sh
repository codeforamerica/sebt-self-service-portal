#!/usr/bin/env bash
# Deploy a CO preview stack (API + Web) into the existing dev-co ECS cluster.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"
# shellcheck source=keycloak.sh
source "${SCRIPT_DIR}/keycloak.sh"

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
  ECR_API_REPOSITORY_URL           API image repository (required)
  ECR_WEB_REPOSITORY_URL           Web image repository (required)
  AWS_REGION                       AWS region (default: us-east-1)
  PREVIEW_KEYCLOAK_HOSTNAME        Shared Keycloak base URL (default: https://auth.<DOMAIN>)
  PREVIEW_KEYCLOAK_DEPLOY_CLIENT_ID Keycloak Admin API client id (default: sebt-preview-deploy)
  PREVIEW_KEYCLOAK_DEPLOY_CLIENT_SECRET Keycloak Admin API client secret
                                   (default: sebt-preview-deploy-secret)
  PREVIEW_KEYCLOAK_DEPLOY_SECRET_ID Optional Secrets Manager id/ARN with JSON
                                   {clientId, clientSecret}
  PREVIEW_OIDC_CLIENT_ID           Keycloak login client id (default: sebt-portal)
  PREVIEW_OIDC_CLIENT_SECRET       Keycloak login client secret (default: realm preview secret)
  PREVIEW_OIDC_STEP_UP_CLIENT_ID   Keycloak step-up client id (default: sebt-portal-stepup)
  PREVIEW_OIDC_STEP_UP_CLIENT_SECRET Keycloak step-up secret (default: realm preview secret)

Notes:
  Web preview hosts are aliased to the shared CloudFront distribution (which
  already reaches the internal web ALB). API preview hosts still alias to the
  API ALB; the Next.js server reaches them via BACKEND_URL.
  Preview stacks use the shared Keycloak IdP for OIDC. After Route53 is created,
  deploy registers the pr-N host as a Valid Redirect URI on the Keycloak clients
  using the sebt-preview-deploy service account (hostname wildcards are not
  supported on Keycloak 26).
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
log_info "Using Route53 hosted zone ${HOSTED_ZONE_ID} for ${DOMAIN}"
API_CLUSTER="$(discover_cluster_for_role api)"
WEB_CLUSTER="$(discover_cluster_for_role web)"
BASE_API_SERVICE="$(discover_base_service api "${API_CLUSTER}")"
BASE_WEB_SERVICE="$(discover_base_service web "${WEB_CLUSTER}")"

API_HOST="api-pr-${PR_NUMBER}.${DOMAIN}"
WEB_HOST="pr-${PR_NUMBER}.${DOMAIN}"
KEYCLOAK_HOSTNAME="${PREVIEW_KEYCLOAK_HOSTNAME:-https://auth.${DOMAIN}}"
KEYCLOAK_DISCOVERY="${KEYCLOAK_HOSTNAME}/realms/sebt/.well-known/openid-configuration"
KEYCLOAK_AUTHORIZE="${KEYCLOAK_HOSTNAME}/realms/sebt/protocol/openid-connect/auth"
OIDC_CLIENT_ID="${PREVIEW_OIDC_CLIENT_ID:-sebt-portal}"
OIDC_CLIENT_SECRET="${PREVIEW_OIDC_CLIENT_SECRET:-sebt-portal-dev-secret}"
OIDC_STEP_UP_CLIENT_ID="${PREVIEW_OIDC_STEP_UP_CLIENT_ID:-sebt-portal-stepup}"
OIDC_STEP_UP_CLIENT_SECRET="${PREVIEW_OIDC_STEP_UP_CLIENT_SECRET:-sebt-portal-stepup-dev-secret}"
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

API_ENV_OVERRIDES="$(jq -n \
  --arg discovery "${KEYCLOAK_DISCOVERY}" \
  --arg authorize "${KEYCLOAK_AUTHORIZE}" \
  --arg callback "https://${WEB_HOST}/callback" \
  --arg client_id "${OIDC_CLIENT_ID}" \
  --arg client_secret "${OIDC_CLIENT_SECRET}" \
  --arg step_up_client_id "${OIDC_STEP_UP_CLIENT_ID}" \
  --arg step_up_client_secret "${OIDC_STEP_UP_CLIENT_SECRET}" \
  '{
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
    "Smarty__Enabled": "false",
    "Oidc__DiscoveryEndpoint": $discovery,
    "Oidc__AuthorizationEndpoint": $authorize,
    "Oidc__CallbackRedirectUri": $callback,
    "Oidc__ClientId": $client_id,
    "Oidc__ClientSecret": $client_secret,
    "Oidc__StepUp__DiscoveryEndpoint": $discovery,
    "Oidc__StepUp__AuthorizationEndpoint": $authorize,
    "Oidc__StepUp__CallbackRedirectUri": $callback,
    "Oidc__StepUp__ClientId": $step_up_client_id,
    "Oidc__StepUp__ClientSecret": $step_up_client_secret
  }')"

# Drop production IdP client secrets from the cloned task so Keycloak env values can replace them.
# Keep Oidc__CompleteLoginSigningKey from the base task (shared HMAC key is fine for previews).
OIDC_STRIP_SECRETS='["Oidc__ClientId","Oidc__ClientSecret","Oidc__StepUp__ClientId","Oidc__StepUp__ClientSecret"]'

WEB_ENV_OVERRIDES="$(jq -n \
  --arg backend_url "https://${API_HOST}" \
  --arg keycloak_origin "${KEYCLOAK_HOSTNAME}" \
  '{
    "STATE": "co",
    "NEXT_PUBLIC_STATE": "co",
    "BACKEND_URL": $backend_url,
    "OIDC_ISSUER_ORIGIN": $keycloak_origin
  }')"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

log_info "Preview OIDC issuer: ${KEYCLOAK_HOSTNAME}"

API_TD_ARN="$(register_preview_task_definition \
  "${API_BASE_TD}" "${API_IMAGE}" "${API_TASK_FAMILY}" "${API_ENV_OVERRIDES}" \
  "${TMP_DIR}/api-task.json" "${API_CONTAINER_NAME}" "${OIDC_STRIP_SECRETS}")"

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

# Public preview traffic goes through CloudFront (VPC origin to the internal web ALB).
# Direct ALB DNS would resolve to private IPs and is not reachable from the internet.
read -r CLOUDFRONT_DNS CLOUDFRONT_ID CLOUDFRONT_ZONE < <(resolve_cloudfront_distribution "${DOMAIN}")
log_info "Routing preview web host through CloudFront distribution ${CLOUDFRONT_ID} (${CLOUDFRONT_DNS})"
ensure_route53_alias \
  "${HOSTED_ZONE_ID}" \
  "${WEB_HOST}" \
  "${CLOUDFRONT_DNS}" \
  "${CLOUDFRONT_ZONE:-${CLOUDFRONT_ROUTE53_ZONE_ID}}" \
  false

# Register after DNS so a Keycloak outage still leaves the preview URL resolvable.
# Still fail the deploy if registration fails; login will not work without it.
ensure_keycloak_preview_host_redirects \
  "${KEYCLOAK_HOSTNAME}" \
  "${WEB_HOST}" \
  "${OIDC_CLIENT_ID}" \
  "${OIDC_STEP_UP_CLIENT_ID}"

# Record deploy marker so PR-close destroy can skip PRs that never deployed a preview.
write_preview_deploy_marker "${IMAGE_TAG}"

PREVIEW_URL="https://${WEB_HOST}"
log_info "Preview URL: ${PREVIEW_URL}"
echo "preview_url=${PREVIEW_URL}"
