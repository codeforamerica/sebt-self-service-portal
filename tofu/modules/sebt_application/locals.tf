locals {
  # Find the Datadog Forwarder Lambda by its CloudFormation naming convention.
  datadog_lambda = [
    for lambda in data.aws_lambda_functions.all.function_names :
    lambda if length(regexall("^DatadogIntegration-ForwarderStack-", lambda)) > 0
  ]

  # CloudWatch log groups to subscribe to the Datadog Forwarder. Only RDS log
  # groups are listed here — the Datadog AWS integration auto-subscribes to
  # ECS and Lambda log groups, but not RDS instance logs.
  datadog_log_groups = {
    database-error = module.database.log_group_names["error"]
    database-agent = module.database.log_group_names["agent"]
  }

  # Datadog does not ingest OpenTelemetry traces from AWS X-Ray. When the
  # Datadog integration API key secret is present, override the default ADOT
  # collector config to forward traces directly to Datadog APM.
  otel_override_config = length(data.aws_secretsmanager_secrets.datadog_key.arns) > 0
  otel_secrets = local.otel_override_config ? {
    DD_API_KEY = data.aws_secretsmanager_secret.datadog_key["this"].arn
  } : {}

  # tofu-modules-aws-fargate-service (module.api) doesn't expose a
  # service_name output — only cluster_name. Its cluster and service happen
  # to share the same name today (HENNGE/ecs/aws: join("-", compact([project,
  # environment, service]))), but that's an implementation detail of the
  # upstream module, not a contract. Reconstruct it explicitly here so a
  # future naming-scheme change in that module doesn't silently break the
  # rotation Lambda's ecs:UpdateService call. If the module ever adds a
  # service_name output, switch to that instead.
  api_ecs_service_name = join("-", compact(["${var.project}-${var.state}", var.environment, "api"]))

  # Allow the AWS Security Agent penetration test to bypass the WAF in the
  # development environment by matching its unique User-Agent. "allow" is a
  # terminating action, so matching requests skip all subsequent rules
  # (including rate limiting). Remove this once the engagement is complete.
  security_agent_waf_rules = var.environment == "development" && var.security_agent_user_agent != "" ? {
    security_agent = {
      paths = [{ constraint = "STARTS_WITH", path = "/" }]
      criteria = [{
        type       = "byte"
        field      = "header"
        name       = "user-agent"
        constraint = "EXACTLY"
        value      = var.security_agent_user_agent
      }]
      action = "allow"
    }
  } : {}
}
