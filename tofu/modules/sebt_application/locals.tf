locals {
  # Find the Datadog Forwarder Lambda by its CloudFormation naming convention.
  datadog_lambda = [
    for lambda in data.aws_lambda_functions.all.function_names :
    lambda if length(regexall("^DatadogIntegration-ForwarderStack-", lambda)) > 0
  ]

  # CloudWatch log groups to subscribe to the Datadog Forwarder. Each entry
  # maps a descriptive key to the log group name. The key encodes the service
  # for easier identification in Datadog.
  datadog_log_groups = {
    api            = module.api.log_group_names[0]
    web            = module.web.log_group_names[0]
    database-error = module.database.log_group_names["error"]
    database-agent = module.database.log_group_names["agent"]
    ses-rotation   = module.ses.log_group_name
  }
}
