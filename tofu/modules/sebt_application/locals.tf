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
