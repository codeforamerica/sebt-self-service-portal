# Adopts the RDS instance and Keycloak that dev-co managed before this config
# existed. Delete after the first apply; Keycloak's random_passwords are seeded into state by hand.

data "aws_caller_identity" "current" {}

import {
  to = module.database.aws_cloudwatch_log_group.agent
  id = "/aws/rds/instance/sebt-portal-co-development-db/agent"
}

import {
  to = module.database.aws_cloudwatch_log_group.error
  id = "/aws/rds/instance/sebt-portal-co-development-db/error"
}

import {
  to = module.database.aws_cloudwatch_log_subscription_filter.datadog["database-agent"]
  id = "/aws/rds/instance/sebt-portal-co-development-db/agent|datadog"
}

import {
  to = module.database.aws_cloudwatch_log_subscription_filter.datadog["database-error"]
  id = "/aws/rds/instance/sebt-portal-co-development-db/error|datadog"
}

import {
  to = module.database.aws_db_instance.main
  id = "sebt-portal-co-development-db"
}

import {
  to = module.database.aws_db_subnet_group.main
  id = "sebt-portal-co-development-db-subnet-group"
}

import {
  to = module.database.aws_kms_alias.database
  id = "alias/sebt-portal-co/development/database"
}

import {
  to = module.database.aws_kms_key.database
  id = "e60d9f8e-80bc-4bc2-8801-fe6cb0ed49ae"
}

import {
  to = module.database.aws_security_group.database
  id = "sg-0fb19db60ed1b372b"
}

import {
  to = module.database.aws_security_group_rule.this["ingress_cidrs"]
  id = "sg-0fb19db60ed1b372b_ingress_tcp_1433_1433_10.0.96.0/22"
}

import {
  to = module.keycloak.aws_db_instance.keycloak
  id = "sebt-portal-co-development-keycloak-db"
}

import {
  to = module.keycloak.aws_db_subnet_group.keycloak
  id = "sebt-portal-co-development-keycloak-db"
}

import {
  to = module.keycloak.aws_lb_listener_rule.admin_allow_header
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:listener-rule/app/sebt-development-kc/0138bcbacdc1655c/2c78e80ba95ab054/3d180ef64e1a7edc"
}

import {
  to = module.keycloak.aws_lb_listener_rule.admin_deny
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:listener-rule/app/sebt-development-kc/0138bcbacdc1655c/2c78e80ba95ab054/a8fbcb5f9484c933"
}

import {
  to = module.keycloak.aws_secretsmanager_secret.admin
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-admin-oPnrBS"
}

import {
  to = module.keycloak.aws_secretsmanager_secret.admin_bypass
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-admin-bypass-20rjJX"
}

import {
  to = module.keycloak.aws_secretsmanager_secret.db
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-db-lV2VzV"
}

import {
  to = module.keycloak.aws_secretsmanager_secret_version.admin
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-admin-oPnrBS|terraform-AAWxka1tQwPR5xl4jy2WRDZQXg"
}

import {
  to = module.keycloak.aws_secretsmanager_secret_version.admin_bypass
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-admin-bypass-20rjJX|terraform-4gHmDcDWEEiYFSmVv9bWFgtlWK"
}

import {
  to = module.keycloak.aws_secretsmanager_secret_version.db
  id = "arn:aws:secretsmanager:us-east-1:${data.aws_caller_identity.current.account_id}:secret:sebt-portal-co-development-keycloak-db-lV2VzV|terraform-cblc4uUGnpV1ArPbAZ6731KZ4n"
}

import {
  to = module.keycloak.aws_security_group.database
  id = "sg-00b8bd227cf616702"
}

import {
  to = module.keycloak.aws_security_group_rule.database_egress
  id = "sg-00b8bd227cf616702_egress_all_0_0_0.0.0.0/0"
}

import {
  to = module.keycloak.aws_security_group_rule.database_ingress
  id = "sg-00b8bd227cf616702_ingress_tcp_5432_5432_sg-00ba3fb3bd1bd125e"
}

import {
  to = module.keycloak.module.service.aws_acm_certificate.endpoint["this"]
  id = "arn:aws:acm:us-east-1:${data.aws_caller_identity.current.account_id}:certificate/edbd39c8-a5a9-45fe-8d73-eb4551b97d3c"
}

import {
  to = module.keycloak.module.service.aws_cloudwatch_log_group.this["service"]
  id = "/aws/ecs/sebt-portal-co/development/keycloak"
}

import {
  to = module.keycloak.module.service.aws_iam_policy.execution
  id = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:policy/sebt-portal-co-development-keycloak-execution"
}

import {
  to = module.keycloak.module.service.aws_iam_policy.secrets
  id = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:policy/sebt-portal-co-development-keycloak-secrets-access-4a546666f2e94f63fd34e1a51a"
}

import {
  to = module.keycloak.module.service.aws_iam_role.execution
  id = "sebt-portal-co-development-keycloak-exec"
}

import {
  to = module.keycloak.module.service.aws_iam_role.task
  id = "sebt-portal-co-development-keycloak-task"
}

import {
  to = module.keycloak.module.service.aws_iam_role_policy_attachments_exclusive.execution
  id = "sebt-portal-co-development-keycloak-exec"
}

import {
  to = module.keycloak.module.service.aws_iam_role_policy_attachments_exclusive.task
  id = "sebt-portal-co-development-keycloak-task"
}

import {
  to = module.keycloak.module.service.aws_kms_alias.fargate
  id = "alias/sebt-portal-co/development/keycloak"
}

import {
  to = module.keycloak.module.service.aws_kms_key.fargate
  id = "cec3f8cb-8867-4bff-90b6-7812f41d6589"
}

import {
  to = module.keycloak.module.service.aws_route53_record.endpoint["this"]
  id = "Z098414410HX6MSQGTBR7_auth.dev.co.sebt-portal.codeforamerica.app_A"
}

import {
  to = module.keycloak.module.service.aws_route53_record.endpoint_validation["auth.dev.co.sebt-portal.codeforamerica.app"]
  id = "Z098414410HX6MSQGTBR7__bde51567796ca8299ee7143a3dc26ee6.auth.dev.co.sebt-portal.codeforamerica.app._CNAME"
}

import {
  to = module.keycloak.module.service.aws_ssm_parameter.otel_config
  id = "/sebt-portal-co/development/keycloak/otel"
}

import {
  to = module.keycloak.module.service.module.alb["this"].aws_lb.this[0]
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:loadbalancer/app/sebt-development-kc/0138bcbacdc1655c"
}

import {
  to = module.keycloak.module.service.module.alb["this"].aws_lb_listener.this["http"]
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:listener/app/sebt-development-kc/0138bcbacdc1655c/2a861060ca8219e7"
}

import {
  to = module.keycloak.module.service.module.alb["this"].aws_lb_listener.this["https"]
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:listener/app/sebt-development-kc/0138bcbacdc1655c/2c78e80ba95ab054"
}

import {
  to = module.keycloak.module.service.module.alb["this"].aws_lb_target_group.this["endpoint"]
  id = "arn:aws:elasticloadbalancing:us-east-1:${data.aws_caller_identity.current.account_id}:targetgroup/sebt-development-kc-app/8ae9fd2969ce2632"
}

import {
  to = module.keycloak.module.service.module.alb["this"].aws_security_group.this[0]
  id = "sg-001f1de70e207669e"
}

import {
  to = module.keycloak.module.service.module.ecs.aws_ecs_cluster.main
  id = "sebt-portal-co-development-keycloak"
}

import {
  to = module.keycloak.module.service.module.ecs.aws_ecs_cluster_capacity_providers.main[0]
  id = "sebt-portal-co-development-keycloak"
}

import {
  to = module.keycloak.module.service.module.ecs_service.module.fargate.aws_ecs_service.main[0]
  id = "sebt-portal-co-development-keycloak/sebt-portal-co-development-keycloak"
}

import {
  to = module.keycloak.module.service.module.ecs_service.module.fargate.module.task.aws_ecs_task_definition.main[0]
  id = "arn:aws:ecs:us-east-1:${data.aws_caller_identity.current.account_id}:task-definition/sebt-portal-co-development-keycloak:5"
}

import {
  to = module.keycloak.module.service.module.endpoint_security_group.aws_security_group.this_name_prefix[0]
  id = "sg-0dcfdb0743098bffc"
}

import {
  to = module.keycloak.module.service.module.endpoint_security_group.aws_security_group_rule.egress_rules[0]
  id = "sg-0dcfdb0743098bffc_egress_all_0_0_10.0.96.0/22"
}

import {
  to = module.keycloak.module.service.module.endpoint_security_group.aws_security_group_rule.ingress_rules[0]
  id = "sg-0dcfdb0743098bffc_ingress_tcp_80_80_0.0.0.0/0"
}

import {
  to = module.keycloak.module.service.module.endpoint_security_group.aws_security_group_rule.ingress_rules[1]
  id = "sg-0dcfdb0743098bffc_ingress_tcp_443_443_0.0.0.0/0"
}

import {
  to = module.keycloak.module.service.module.task_security_group.aws_security_group.this_name_prefix[0]
  id = "sg-00ba3fb3bd1bd125e"
}

import {
  to = module.keycloak.module.service.module.task_security_group.aws_security_group_rule.egress_rules[0]
  id = "sg-00ba3fb3bd1bd125e_egress_all_0_0_0.0.0.0/0_::/0"
}

import {
  to = module.keycloak.module.service.module.task_security_group.aws_security_group_rule.ingress_with_source_security_group_id[0]
  id = "sg-00ba3fb3bd1bd125e_ingress_tcp_8080_8080_sg-0dcfdb0743098bffc"
}
