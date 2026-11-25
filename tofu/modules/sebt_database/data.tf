data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}
data "aws_region" "current" {}

data "aws_rds_engine_version" "this" {
  engine  = var.engine
  version = var.engine_version
  latest  = true
}

# Find the Datadog Forwarder Lambda so we can subscribe RDS CloudWatch log
# groups to it. The forwarder is deployed separately (via Datadog's
# CloudFormation stack) and discovered here by its naming convention. If it
# doesn't exist yet, no subscription filters will be created.
data "aws_lambda_functions" "all" {}

data "aws_lambda_function" "datadog" {
  for_each = length(local.datadog_lambda) > 0 ? toset(["this"]) : toset([])

  function_name = local.datadog_lambda[0]
}
