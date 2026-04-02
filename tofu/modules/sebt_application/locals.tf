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
}
