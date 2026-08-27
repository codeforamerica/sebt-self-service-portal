# Restrict Keycloak Admin Console / Admin REST API on the public ALB.
#
# OIDC discovery and /realms/* stay public. /admin and /admin/* return 403 unless
# the request matches an allow rule (bypass header used by preview deploy scripts,
# and/or source IP CIDRs for break-glass console access).

locals {
  admin_bypass_header_name = "x-sebt-keycloak-admin-bypass"
  # ALB source-ip conditions allow at most five CIDRs per rule.
  admin_cidr_chunks = chunklist(var.admin_ingress_cidrs, 5)
  # Fargate-service short name: project_short-environment-service_short-app
  keycloak_target_group_name = "sebt-${var.environment}-kc-app"
}

resource "random_password" "admin_bypass" {
  length  = 48
  special = false
}

resource "aws_secretsmanager_secret" "admin_bypass" {
  name                    = "${local.prefix}-admin-bypass"
  recovery_window_in_days = var.force_delete ? 0 : 7
}

resource "aws_secretsmanager_secret_version" "admin_bypass" {
  secret_id = aws_secretsmanager_secret.admin_bypass.id
  secret_string = jsonencode({
    headerName  = local.admin_bypass_header_name
    headerValue = random_password.admin_bypass.result
  })
}

data "aws_lb_listener" "https" {
  load_balancer_arn = module.service.load_balancer_arn
  port              = 443

  depends_on = [module.service]
}

data "aws_lb_target_group" "keycloak" {
  name = local.keycloak_target_group_name

  # Name alone would be readable at plan before the TG exists; wait for the service.
  depends_on = [module.service]
}

resource "aws_lb_listener_rule" "admin_allow_header" {
  listener_arn = data.aws_lb_listener.https.arn
  priority     = 10

  action {
    type             = "forward"
    target_group_arn = data.aws_lb_target_group.keycloak.arn
  }

  condition {
    path_pattern {
      values = ["/admin", "/admin/*"]
    }
  }

  condition {
    http_header {
      http_header_name = local.admin_bypass_header_name
      values           = [random_password.admin_bypass.result]
    }
  }
}

resource "aws_lb_listener_rule" "admin_allow_cidr" {
  for_each = {
    for idx, cidrs in local.admin_cidr_chunks : tostring(idx) => cidrs
    if length(cidrs) > 0
  }

  listener_arn = data.aws_lb_listener.https.arn
  priority     = 20 + tonumber(each.key)

  action {
    type             = "forward"
    target_group_arn = data.aws_lb_target_group.keycloak.arn
  }

  condition {
    path_pattern {
      values = ["/admin", "/admin/*"]
    }
  }

  condition {
    source_ip {
      values = each.value
    }
  }
}

resource "aws_lb_listener_rule" "admin_deny" {
  listener_arn = data.aws_lb_listener.https.arn
  # After allow-header (10) and allow-cidr (20..24).
  priority = 40

  action {
    type = "fixed-response"

    fixed_response {
      content_type = "text/plain"
      message_body = "Forbidden"
      status_code  = "403"
    }
  }

  condition {
    path_pattern {
      values = ["/admin", "/admin/*"]
    }
  }
}
