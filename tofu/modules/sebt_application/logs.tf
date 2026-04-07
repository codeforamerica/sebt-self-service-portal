# Subscribe RDS log groups to the Datadog Forwarder Lambda. The Datadog AWS
# integration auto-subscribes ECS and Lambda log groups but not RDS instance
# logs, so we handle those explicitly. If the forwarder hasn't been deployed
# yet, no subscriptions are created.
resource "aws_cloudwatch_log_subscription_filter" "datadog" {
  for_each = length(local.datadog_lambda) > 0 ? local.datadog_log_groups : {}

  name            = "datadog"
  log_group_name  = each.value
  filter_pattern  = ""
  destination_arn = data.aws_lambda_function.datadog["this"].arn
}
