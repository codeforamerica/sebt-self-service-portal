#!/usr/bin/env bash
# Tear down a CO preview stack created by deploy-co.sh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

usage() {
  cat <<'EOF'
Usage: destroy-co.sh <pr-number> [--force]

Environment:
  PREVIEW_DOMAIN / DOMAIN          Public preview domain (required)
  PREVIEW_HOSTED_ZONE_ID           Route53 hosted zone ID (optional; auto-discovered)
  PREVIEW_ECS_CLUSTER              ECS cluster name (optional; auto-discovered)
  PREVIEW_BASE_API_SERVICE         Base dev-co API ECS service name (optional)
  PREVIEW_BASE_WEB_SERVICE         Base dev-co Web ECS service name (optional)
  PREVIEW_API_SERVICE              Alias for PREVIEW_BASE_API_SERVICE (optional)
  PREVIEW_WEB_SERVICE              Alias for PREVIEW_BASE_WEB_SERVICE (optional)
  AWS_REGION                       AWS region (default: us-east-1)

Without --force, skips teardown when no deploy marker or preview resources exist.
EOF
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
  usage
  exit 0
fi

PR_NUMBER="${1:-}"
FORCE_DESTROY=false
if [ "${2:-}" = "--force" ]; then
  FORCE_DESTROY=true
fi

if [ -z "${PR_NUMBER}" ]; then
  usage
  exit 1
fi

preview_requirements

export AWS_REGION="${AWS_REGION:-us-east-1}"
export PR_NUMBER

STACK_API_SERVICE="sebt-co-preview-pr-${PR_NUMBER}-api"
STACK_WEB_SERVICE="sebt-co-preview-pr-${PR_NUMBER}-web"
API_TG_NAME="co-pr-${PR_NUMBER}-api"
WEB_TG_NAME="co-pr-${PR_NUMBER}-web"

if [ "${FORCE_DESTROY}" != true ] && ! preview_deploy_marker_exists; then
  API_CLUSTER="$(discover_cluster_for_role api)"
  WEB_CLUSTER="$(discover_cluster_for_role web)"
  if ! preview_stack_resources_exist \
    "${API_CLUSTER}" "${WEB_CLUSTER}" \
    "${STACK_API_SERVICE}" "${STACK_WEB_SERVICE}" "${API_TG_NAME}" "${WEB_TG_NAME}"; then
    log_info "No preview deploy marker or resources for PR ${PR_NUMBER}; skipping destroy"
    exit 0
  fi
fi

DOMAIN="$(resolve_preview_domain)"
HOSTED_ZONE_ID="$(resolve_hosted_zone_id "${DOMAIN}")"
log_info "Using Route53 hosted zone ${HOSTED_ZONE_ID} for ${DOMAIN}"
API_CLUSTER="$(discover_cluster_for_role api)"
WEB_CLUSTER="$(discover_cluster_for_role web)"
BASE_API_SERVICE="$(discover_base_service api "${API_CLUSTER}")"
BASE_WEB_SERVICE="$(discover_base_service web "${WEB_CLUSTER}")"

API_HOST="api-pr-${PR_NUMBER}.${DOMAIN}"
WEB_HOST="pr-${PR_NUMBER}.${DOMAIN}"

delete_route53_record() {
  local hosted_zone_id="$1"
  local record_name="$2"
  local alb_dns_name="$3"
  local alb_zone_id="$4"

  aws route53 change-resource-record-sets \
    --hosted-zone-id "${hosted_zone_id}" \
    --change-batch "$(jq -n \
      --arg record_name "${record_name}" \
      --arg alb_dns_name "${alb_dns_name}" \
      --arg alb_zone_id "${alb_zone_id}" \
      '{
        "Changes": [{
          "Action": "DELETE",
          "ResourceRecordSet": {
            "Name": $record_name,
            "Type": "A",
            "AliasTarget": {
              "HostedZoneId": $alb_zone_id,
              "DNSName": $alb_dns_name,
              "EvaluateTargetHealth": true
            }
          }
        }]
      }')" 2>/dev/null || true
}

delete_ecs_service_if_exists() {
  local cluster="$1"
  local service_name="$2"
  local description status failure_reason

  description="$(describe_preview_service "${cluster}" "${service_name}")"
  preview_service_status "${cluster}" "${service_name}" "${description}" status failure_reason

  if [ "${failure_reason}" = "MISSING" ] || [ -z "${status}" ]; then
    return
  fi

  case "${status}" in
    ACTIVE)
      log_info "Deleting ECS service ${service_name}"
      aws ecs update-service \
        --cluster "${cluster}" \
        --service "${service_name}" \
        --desired-count 0 >/dev/null
      aws ecs wait services-stable \
        --cluster "${cluster}" \
        --services "${service_name}" 2>/dev/null || true
      aws ecs delete-service \
        --cluster "${cluster}" \
        --service "${service_name}" \
        --force >/dev/null
      wait_for_ecs_service_inactive "${cluster}" "${service_name}"
      ;;
    DRAINING)
      log_info "Waiting for ECS service ${service_name} to finish draining"
      wait_for_ecs_service_inactive "${cluster}" "${service_name}"
      ;;
    INACTIVE)
      log_info "Waiting for ECS service ${service_name} name to clear (INACTIVE)"
      wait_for_ecs_service_inactive "${cluster}" "${service_name}"
      ;;
    *)
      log_info "Skipping ECS service ${service_name} with status ${status}"
      ;;
  esac
}

delete_listener_rule_if_exists() {
  local listener_arn="$1"
  local host="$2"
  local rule_arn
  rule_arn="$(find_listener_rule_for_host "${listener_arn}" "${host}")"
  if [ -n "${rule_arn}" ]; then
    log_info "Deleting listener rule for ${host}"
    aws elbv2 delete-rule --rule-arn "${rule_arn}" >/dev/null
  fi
}

delete_target_group_if_exists() {
  local name="$1"
  local tg_arn
  tg_arn="$(aws elbv2 describe-target-groups --names "${name}" \
    --query 'TargetGroups[0].TargetGroupArn' --output text 2>/dev/null || echo "")"
  if [ -n "${tg_arn}" ] && [ "${tg_arn}" != "None" ]; then
    log_info "Deleting target group ${name}"
    aws elbv2 delete-target-group --target-group-arn "${tg_arn}" >/dev/null || true
  fi
}

revoke_preview_https_ingress() {
  local security_group_id="$1"
  local description="sebt-preview-direct-https-pr-${PR_NUMBER}"
  local rule_ids
  rule_ids="$(aws ec2 describe-security-group-rules \
    --filters "Name=group-id,Values=${security_group_id}" \
    --query "SecurityGroupRules[?Description=='${description}'].SecurityGroupRuleId" \
    --output text)"

  for rule_id in ${rule_ids}; do
    [ -n "${rule_id}" ] || continue
    log_info "Revoking preview ingress rule ${rule_id}"
    aws ec2 revoke-security-group-ingress \
      --group-id "${security_group_id}" \
      --security-group-rule-ids "${rule_id}" >/dev/null || true
  done
}

log_info "Destroying preview for PR ${PR_NUMBER}"

delete_ecs_service_if_exists "${API_CLUSTER}" "${STACK_API_SERVICE}"
delete_ecs_service_if_exists "${WEB_CLUSTER}" "${STACK_WEB_SERVICE}"

API_LB="$(get_service_load_balancer "${API_CLUSTER}" "${BASE_API_SERVICE}")"
WEB_LB="$(get_service_load_balancer "${WEB_CLUSTER}" "${BASE_WEB_SERVICE}")"
API_BASE_TG="$(echo "${API_LB}" | jq -r '.targetGroupArn')"
WEB_BASE_TG="$(echo "${WEB_LB}" | jq -r '.targetGroupArn')"
API_ALB_ARN="$(get_target_group_lb_arn "${API_BASE_TG}")"
WEB_ALB_ARN="$(get_target_group_lb_arn "${WEB_BASE_TG}")"
API_LISTENER_ARN="$(get_https_listener_arn "${API_ALB_ARN}")"
WEB_LISTENER_ARN="$(get_https_listener_arn "${WEB_ALB_ARN}")"

delete_listener_rule_if_exists "${API_LISTENER_ARN}" "${API_HOST}"
delete_listener_rule_if_exists "${WEB_LISTENER_ARN}" "${WEB_HOST}"

delete_target_group_if_exists "${API_TG_NAME}"
delete_target_group_if_exists "${WEB_TG_NAME}"

delete_route53_record \
  "${HOSTED_ZONE_ID}" \
  "${API_HOST}" \
  "$(get_alb_dns_name "${API_ALB_ARN}")" \
  "$(get_alb_hosted_zone_id "${API_ALB_ARN}")"

delete_route53_record \
  "${HOSTED_ZONE_ID}" \
  "${WEB_HOST}" \
  "$(get_alb_dns_name "${WEB_ALB_ARN}")" \
  "$(get_alb_hosted_zone_id "${WEB_ALB_ARN}")"

while IFS= read -r security_group_id; do
  [ -n "${security_group_id}" ] || continue
  revoke_preview_https_ingress "${security_group_id}"
done < <(get_alb_security_groups "${WEB_ALB_ARN}" | jq -r '.[]')

delete_preview_deploy_marker

log_info "Preview teardown complete for PR ${PR_NUMBER}"
