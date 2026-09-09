#!/usr/bin/env bash
# Shared helpers for CO preview deploy/destroy scripts.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

log_info() {
  echo "[preview] $*" >&2
}

log_error() {
  echo "[preview] ERROR: $*" >&2
}

require_command() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    log_error "Required command not found: ${cmd}"
    exit 1
  fi
}

preview_requirements() {
  require_command aws
  require_command jq
  require_command python3
  require_command curl
}

resolve_preview_domain() {
  if [ -n "${PREVIEW_DOMAIN:-}" ]; then
    echo "${PREVIEW_DOMAIN}"
    return
  fi
  if [ -n "${DOMAIN:-}" ]; then
    echo "${DOMAIN}"
    return
  fi
  log_error "PREVIEW_DOMAIN or DOMAIN must be set"
  exit 1
}

# ALB listener priorities must be unique per listener. Reserve 20000-49998 for previews
# (two consecutive priorities per PR: API host, then Web host).
preview_listener_priority() {
  local pr_number="$1"
  local slot="$2"

  if ! [[ "${pr_number}" =~ ^[0-9]+$ ]]; then
    log_error "PR number must be numeric: ${pr_number}"
    exit 1
  fi

  # Max PR 14998 keeps slot 1 (web) at priority 49998 within the reserved range.
  if [ "${pr_number}" -gt 14998 ]; then
    log_error "PR number ${pr_number} exceeds preview listener priority range (max 14998)"
    exit 1
  fi

  echo $((20000 + pr_number * 2 + slot))
}

discover_cluster_for_role() {
  local role="$1"

  if [ "${role}" = "api" ] && [ -n "${PREVIEW_API_ECS_CLUSTER:-}" ]; then
    echo "${PREVIEW_API_ECS_CLUSTER}"
    return
  fi
  if [ "${role}" = "web" ] && [ -n "${PREVIEW_WEB_ECS_CLUSTER:-}" ]; then
    echo "${PREVIEW_WEB_ECS_CLUSTER}"
    return
  fi
  if [ -n "${PREVIEW_ECS_CLUSTER:-}" ]; then
    echo "${PREVIEW_ECS_CLUSTER}"
    return
  fi

  local cluster_arn
  cluster_arn="$(aws ecs list-clusters --output text \
    | tr '\t' '\n' \
    | grep -Ei "sebt.*co.*development.*-${role}\$" \
    | head -n 1 || true)"

  if [ -z "${cluster_arn}" ]; then
    cluster_arn="$(aws ecs list-clusters --output text \
      | tr '\t' '\n' \
      | grep -Ei 'sebt.*co.*development' \
      | grep -viE '-(api|web)\$' \
      | head -n 1 || true)"
  fi

  if [ -z "${cluster_arn}" ]; then
    log_error "Could not discover ECS ${role} cluster. Set PREVIEW_API_ECS_CLUSTER / PREVIEW_WEB_ECS_CLUSTER."
    exit 1
  fi

  basename "${cluster_arn}"
}

discover_cluster() {
  discover_cluster_for_role "api"
}

discover_base_service() {
  local role="$1"
  local cluster="$2"

  if [ "${role}" = "api" ] && [ -n "${PREVIEW_BASE_API_SERVICE:-}" ]; then
    echo "${PREVIEW_BASE_API_SERVICE}"
    return
  fi
  if [ "${role}" = "web" ] && [ -n "${PREVIEW_BASE_WEB_SERVICE:-}" ]; then
    echo "${PREVIEW_BASE_WEB_SERVICE}"
    return
  fi

  # Backward-compatible aliases for the base (dev-co) ECS services.
  if [ "${role}" = "api" ] && [ -n "${PREVIEW_API_SERVICE:-}" ]; then
    echo "${PREVIEW_API_SERVICE}"
    return
  fi
  if [ "${role}" = "web" ] && [ -n "${PREVIEW_WEB_SERVICE:-}" ]; then
    echo "${PREVIEW_WEB_SERVICE}"
    return
  fi

  local candidates service_arn pattern candidate_count
  candidates="$(aws ecs list-services --cluster "${cluster}" --output text \
    | tr '\t' '\n' \
    | grep -vi preview \
    || true)"

  candidate_count="$(echo "${candidates}" | sed '/^$/d' | wc -l | tr -d ' ')"
  if [ "${candidate_count}" = "1" ]; then
    basename "$(echo "${candidates}" | head -n 1)"
    return
  fi

  for pattern in \
    "development-${role}\$" \
    "-${role}\$" \
    "sebt.*co.*${role}\$"; do
    service_arn="$(echo "${candidates}" | grep -Ei "${pattern}" | head -n 1 || true)"
    if [ -n "${service_arn}" ]; then
      basename "${service_arn}"
      return
    fi
  done

  log_error "Could not discover base ${role} service in cluster ${cluster}. Set PREVIEW_BASE_API_SERVICE / PREVIEW_BASE_WEB_SERVICE."
  exit 1
}

get_service_task_definition() {
  local cluster="$1"
  local service="$2"
  aws ecs describe-services \
    --cluster "${cluster}" \
    --services "${service}" \
    --query 'services[0].taskDefinition' \
    --output text
}

get_service_load_balancer() {
  local cluster="$1"
  local service="$2"
  aws ecs describe-services \
    --cluster "${cluster}" \
    --services "${service}" \
    --query 'services[0].loadBalancers[0]' \
    --output json
}

get_target_group_lb_arn() {
  local target_group_arn="$1"
  aws elbv2 describe-target-groups \
    --target-group-arns "${target_group_arn}" \
    --query 'TargetGroups[0].LoadBalancerArns[0]' \
    --output text
}

get_https_listener_arn() {
  local load_balancer_arn="$1"
  aws elbv2 describe-listeners \
    --load-balancer-arn "${load_balancer_arn}" \
    --query 'Listeners[?Port==`443`].ListenerArn | [0]' \
    --output text
}

get_alb_security_groups() {
  local load_balancer_arn="$1"
  aws elbv2 describe-load-balancers \
    --load-balancer-arns "${load_balancer_arn}" \
    --query 'LoadBalancers[0].SecurityGroups' \
    --output json
}

get_alb_vpc_id() {
  local load_balancer_arn="$1"
  aws elbv2 describe-load-balancers \
    --load-balancer-arns "${load_balancer_arn}" \
    --query 'LoadBalancers[0].VpcId' \
    --output text
}

get_alb_dns_name() {
  local load_balancer_arn="$1"
  aws elbv2 describe-load-balancers \
    --load-balancer-arns "${load_balancer_arn}" \
    --query 'LoadBalancers[0].DNSName' \
    --output text
}

get_alb_hosted_zone_id() {
  local load_balancer_arn="$1"
  aws elbv2 describe-load-balancers \
    --load-balancer-arns "${load_balancer_arn}" \
    --query 'LoadBalancers[0].CanonicalHostedZoneId' \
    --output text
}

register_preview_task_definition() {
  local base_task_definition="$1"
  local image="$2"
  local family="$3"
  local env_overrides_json="$4"
  local output_file="$5"
  local container_name="${6:-}"
  local strip_secret_names_json="${7:-}"

  local merge_args=("${env_overrides_json}" "${image}" "${family}" "--strip-sidecars")
  if [ -n "${strip_secret_names_json}" ]; then
    merge_args+=("--strip-secret-names" "${strip_secret_names_json}")
  fi
  if [ -n "${container_name}" ]; then
    merge_args+=("${container_name}")
  fi

  aws ecs describe-task-definition \
    --task-definition "${base_task_definition}" \
    --output json \
    | python3 "${SCRIPT_DIR}/merge_task_definition.py" "${merge_args[@]}" \
    > "${output_file}"

  aws ecs register-task-definition --cli-input-json "file://${output_file}" \
    --query 'taskDefinition.taskDefinitionArn' \
    --output text
}

ensure_target_group() {
  local name="$1"
  local port="$2"
  local health_path="$3"
  local vpc_id="$4"
  local existing_arn="$5"

  if [ -n "${existing_arn}" ] && [ "${existing_arn}" != "None" ]; then
    echo "${existing_arn}"
    return
  fi

  aws elbv2 create-target-group \
    --name "${name}" \
    --protocol HTTP \
    --port "${port}" \
    --vpc-id "${vpc_id}" \
    --target-type ip \
    --health-check-path "${health_path}" \
    --health-check-interval-seconds 30 \
    --health-check-timeout-seconds 10 \
    --healthy-threshold-count 2 \
    --unhealthy-threshold-count 3 \
    --tags "Key=sebt-preview,Value=true" "Key=sebt-preview-pr,Value=${PR_NUMBER}" \
    --query 'TargetGroups[0].TargetGroupArn' \
    --output text
}

ensure_listener_rule() {
  local listener_arn="$1"
  local priority="$2"
  local host="$3"
  local target_group_arn="$4"
  local existing_rule_arn="$5"

  if [ -n "${existing_rule_arn}" ] && [ "${existing_rule_arn}" != "None" ]; then
    aws elbv2 modify-rule \
      --rule-arn "${existing_rule_arn}" \
      --conditions "Field=host-header,Values=${host}" \
      --actions "Type=forward,TargetGroupArn=${target_group_arn}"
    echo "${existing_rule_arn}"
    return
  fi

  aws elbv2 create-rule \
    --listener-arn "${listener_arn}" \
    --priority "${priority}" \
    --conditions "Field=host-header,Values=${host}" \
    --actions "Type=forward,TargetGroupArn=${target_group_arn}" \
    --tags "Key=sebt-preview,Value=true" "Key=sebt-preview-pr,Value=${PR_NUMBER}" \
    --query 'Rules[0].RuleArn' \
    --output text
}

find_listener_rule_for_host() {
  local listener_arn="$1"
  local host="$2"
  aws elbv2 describe-rules \
    --listener-arn "${listener_arn}" \
    --output json \
    | jq -r --arg host "${host}" '
        .Rules[]
        | select(.Conditions[]? | select(.Field == "host-header") | .Values[]? == $host)
        | .RuleArn' \
    | head -n 1
}

describe_preview_service() {
  local cluster="$1"
  local service_name="$2"
  aws ecs describe-services \
    --cluster "${cluster}" \
    --services "${service_name}" \
    --output json
}

preview_service_status() {
  local cluster="$1"
  local service_name="$2"
  local description="$3"
  local status_var="$4"
  local failure_var="$5"

  if [ -z "${description}" ]; then
    description="$(describe_preview_service "${cluster}" "${service_name}")"
  fi

  printf -v "${status_var}" '%s' "$(echo "${description}" | jq -r '.services[0].status // empty')"
  printf -v "${failure_var}" '%s' "$(echo "${description}" | jq -r '.failures[0].reason // empty')"
}

ecs_create_service_name_blocked() {
  echo "$1" | grep -qiE \
    'not idempotent|still contains an inactive|Creation of service was not idempotent|Create service is not idempotent'
}

recreate_preview_ecs_service() {
  local cluster="$1"
  local service_name="$2"
  local task_definition="$3"
  local target_group_arn="$4"
  local container_name="$5"
  local container_port="$6"
  local network_config_file="$7"
  local max_attempts="${8:-90}"

  local attempt create_output create_status

  for attempt in $(seq 1 "${max_attempts}"); do
    set +e
    create_output="$(create_preview_ecs_service \
      "${cluster}" "${service_name}" "${task_definition}" "${target_group_arn}" \
      "${container_name}" "${container_port}" "${network_config_file}" 2>&1)"
    create_status=$?
    set -e

    if [ "${create_status}" -eq 0 ]; then
      echo "${create_output}"
      return 0
    fi

    if ecs_create_service_name_blocked "${create_output}"; then
      log_info "ECS service name ${service_name} not yet reusable (attempt ${attempt}/${max_attempts})..."
      sleep 10
      continue
    fi

    log_error "Failed to create ECS service ${service_name}: ${create_output}"
    exit 1
  done

  log_error "Timed out recreating ECS service ${service_name} after ${max_attempts} attempts"
  exit 1
}

create_preview_ecs_service() {
  local cluster="$1"
  local service_name="$2"
  local task_definition="$3"
  local target_group_arn="$4"
  local container_name="$5"
  local container_port="$6"
  local network_config_file="$7"

  aws ecs create-service \
    --cluster "${cluster}" \
    --service-name "${service_name}" \
    --task-definition "${task_definition}" \
    --desired-count 1 \
    --launch-type FARGATE \
    --network-configuration "file://${network_config_file}" \
    --load-balancers "targetGroupArn=${target_group_arn},containerName=${container_name},containerPort=${container_port}" \
    --tags key=sebt-preview,value=true key=sebt-preview-pr,value="${PR_NUMBER}" \
    --query 'service.serviceName' \
    --output text
}

ensure_ecs_service() {
  local cluster="$1"
  local service_name="$2"
  local task_definition="$3"
  local target_group_arn="$4"
  local container_name="$5"
  local container_port="$6"
  local base_service="$7"
  local network_config_file="$8"

  local network_configuration
  network_configuration="$(aws ecs describe-services \
    --cluster "${cluster}" \
    --services "${base_service}" \
    --query 'services[0].networkConfiguration' \
    --output json)"

  echo "${network_configuration}" > "${network_config_file}"

  local description existing_status failure_reason
  description="$(describe_preview_service "${cluster}" "${service_name}")"
  preview_service_status "${cluster}" "${service_name}" "${description}" existing_status failure_reason

  if [ "${failure_reason}" = "MISSING" ] || [ -z "${existing_status}" ]; then
    create_preview_ecs_service \
      "${cluster}" "${service_name}" "${task_definition}" "${target_group_arn}" \
      "${container_name}" "${container_port}" "${network_config_file}"
    return
  fi

  case "${existing_status}" in
    ACTIVE)
      aws ecs update-service \
        --cluster "${cluster}" \
        --service "${service_name}" \
        --task-definition "${task_definition}" \
        --desired-count 1 \
        --force-new-deployment \
        --query 'service.serviceName' \
        --output text
      ;;
    DRAINING|INACTIVE)
      log_info "ECS service ${service_name} is ${existing_status}; recreating"
      if [ "${existing_status}" = "DRAINING" ]; then
        aws ecs wait services-inactive \
          --cluster "${cluster}" \
          --services "${service_name}" 2>/dev/null || true
      fi
      recreate_preview_ecs_service \
        "${cluster}" "${service_name}" "${task_definition}" "${target_group_arn}" \
        "${container_name}" "${container_port}" "${network_config_file}"
      ;;
    *)
      log_error "Unexpected ECS service status for ${service_name}: ${existing_status}"
      exit 1
      ;;
  esac
}

wait_for_preview_services_stable() {
  local api_cluster="$1"
  local web_cluster="$2"
  local api_service="$3"
  local web_service="$4"

  log_info "Waiting for preview ECS services to stabilize"
  aws ecs wait services-stable \
    --cluster "${api_cluster}" \
    --services "${api_service}"
  aws ecs wait services-stable \
    --cluster "${web_cluster}" \
    --services "${web_service}"
}

ensure_route53_alias() {
  local hosted_zone_id="$1"
  local record_name="$2"
  local target_dns_name="$3"
  local target_zone_id="$4"
  local evaluate_target_health="${5:-true}"

  aws route53 change-resource-record-sets \
    --hosted-zone-id "${hosted_zone_id}" \
    --change-batch "$(jq -n \
      --arg record_name "${record_name}" \
      --arg target_dns_name "${target_dns_name}" \
      --arg target_zone_id "${target_zone_id}" \
      --argjson evaluate_target_health "${evaluate_target_health}" \
      '{
        "Changes": [{
          "Action": "UPSERT",
          "ResourceRecordSet": {
            "Name": $record_name,
            "Type": "A",
            "AliasTarget": {
              "HostedZoneId": $target_zone_id,
              "DNSName": $target_dns_name,
              "EvaluateTargetHealth": $evaluate_target_health
            }
          }
        }]
      }')"
}

# CloudFront alias records always use this Route53 hosted zone ID.
CLOUDFRONT_ROUTE53_ZONE_ID="Z2FDTNDATAQYW2"

# Resolve the CloudFront distribution that serves the given domain (exact alias
# or a matching wildcard alternate domain name such as *.example.org).
resolve_cloudfront_distribution() {
  local domain="$1"
  local match

  match="$(aws cloudfront list-distributions --output json \
    | jq -r --arg domain "${domain}" '
      .DistributionList.Items[]?
      | select(.Aliases.Items != null)
      | select(
          (.Aliases.Items | index($domain) != null)
          or (
            .Aliases.Items
            | map(select(startswith("*.")))
            | map(.[2:])
            | map(select($domain == . or ($domain | endswith("." + .))))
            | length > 0
          )
        )
      | [.DomainName, .Id, (.HostedZoneId // "Z2FDTNDATAQYW2")]
      | @tsv
    ' | head -n1)"

  if [ -z "${match}" ]; then
    log_error "Could not find CloudFront distribution for ${domain}. Ensure tofu applied cloudfront_extra_aliases (for example *.${domain#*.})."
    exit 1
  fi

  echo "${match}"
}

normalize_hosted_zone_id() {
  local value="$1"
  value="$(printf '%s' "${value}" | head -n1 | tr -d '[:space:]')"
  if [[ ! "${value}" =~ ^Z[A-Z0-9]+$ ]]; then
    log_error "Invalid Route53 hosted zone ID: ${value}"
    exit 1
  fi
  echo "${value}"
}

resolve_hosted_zone_id() {
  local domain="$1"
  local zone_id=""

  if [ -n "${PREVIEW_HOSTED_ZONE_ID:-}" ]; then
    if [[ "${PREVIEW_HOSTED_ZONE_ID}" =~ ^Z[A-Z0-9]+$ ]]; then
      normalize_hosted_zone_id "${PREVIEW_HOSTED_ZONE_ID}"
      return
    fi

    local configured_zone="${PREVIEW_HOSTED_ZONE_ID}"
    if [[ "${configured_zone}" != *\. ]]; then
      configured_zone="${configured_zone}."
    fi

    zone_id="$(aws route53 list-hosted-zones-by-name \
      --dns-name "${configured_zone}" \
      --query 'HostedZones[?Name==`'"${configured_zone}"'`].Id | [0]' \
      --output text \
      | sed 's|/hostedzone/||' \
      | head -n1 \
      | tr -d '[:space:]')"

    if [ -n "${zone_id}" ] && [ "${zone_id}" != "None" ]; then
      normalize_hosted_zone_id "${zone_id}"
      return
    fi

    log_error "PREVIEW_HOSTED_ZONE_ID is not a zone ID or known zone name: ${PREVIEW_HOSTED_ZONE_ID}"
    exit 1
  fi

  local domain_dot="${domain}."
  local best_zone_id=""
  local best_zone_name=""
  local candidate_id candidate_name

  while IFS=$'\t' read -r candidate_id candidate_name; do
    candidate_id="${candidate_id#/hostedzone/}"
    if [ "${domain_dot}" = "${candidate_name}" ] || [[ "${domain_dot}" == *".${candidate_name}" ]]; then
      if [ -z "${best_zone_name}" ] || [ "${#candidate_name}" -gt "${#best_zone_name}" ]; then
        best_zone_id="${candidate_id}"
        best_zone_name="${candidate_name}"
      fi
    fi
  done < <(aws route53 list-hosted-zones --output json \
    | jq -r '.HostedZones[] | [.Id, .Name] | @tsv')

  if [ -z "${best_zone_id}" ]; then
    log_error "Could not find Route53 hosted zone containing ${domain}. Set PREVIEW_HOSTED_ZONE_ID."
    exit 1
  fi

  normalize_hosted_zone_id "${best_zone_id}"
}

preview_ssm_param_name() {
  echo "/sebt/co/preview/pr-${PR_NUMBER}"
}

write_preview_deploy_marker() {
  local image_tag="$1"
  local param_name
  param_name="$(preview_ssm_param_name)"

  if preview_deploy_marker_exists; then
    aws ssm put-parameter \
      --name "${param_name}" \
      --value "${image_tag}" \
      --type String \
      --overwrite >/dev/null
    return
  fi

  aws ssm put-parameter \
    --name "${param_name}" \
    --value "${image_tag}" \
    --type String \
    --tags "Key=sebt-preview,Value=true" "Key=sebt-preview-pr,Value=${PR_NUMBER}" >/dev/null
}

preview_deploy_marker_exists() {
  aws ssm get-parameter --name "$(preview_ssm_param_name)" >/dev/null 2>&1
}

delete_preview_deploy_marker() {
  aws ssm delete-parameter --name "$(preview_ssm_param_name)" 2>/dev/null || true
}

preview_stack_resources_exist() {
  local api_cluster="$1"
  local web_cluster="$2"
  local api_service="$3"
  local web_service="$4"
  local api_tg_name="$5"
  local web_tg_name="$6"
  local description status failure_reason

  description="$(describe_preview_service "${api_cluster}" "${api_service}")"
  preview_service_status "${api_cluster}" "${api_service}" "${description}" status failure_reason
  if [ -n "${status}" ] && [ "${failure_reason}" != "MISSING" ]; then
    return 0
  fi

  description="$(describe_preview_service "${web_cluster}" "${web_service}")"
  preview_service_status "${web_cluster}" "${web_service}" "${description}" status failure_reason
  if [ -n "${status}" ] && [ "${failure_reason}" != "MISSING" ]; then
    return 0
  fi

  if aws elbv2 describe-target-groups --names "${api_tg_name}" >/dev/null 2>&1; then
    return 0
  fi
  if aws elbv2 describe-target-groups --names "${web_tg_name}" >/dev/null 2>&1; then
    return 0
  fi

  return 1
}

wait_for_ecs_service_inactive() {
  local cluster="$1"
  local service_name="$2"

  aws ecs wait services-inactive \
    --cluster "${cluster}" \
    --services "${service_name}" 2>/dev/null || true
}
