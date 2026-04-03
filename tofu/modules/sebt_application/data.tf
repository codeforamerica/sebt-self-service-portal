# Prefix lists can be used in security group rules to allow traffic from
# known AWS service IP ranges without needing to hardcode them. Here, we look
# up the managed prefix list for CloudFront origin-facing IPs so that we can
# allow them in our load balancer security rules.
# See: https://docs.aws.amazon.com/vpc/latest/userguide/working-with-aws-managed-prefix-lists.html
data "aws_ec2_managed_prefix_list" "cloudfront" {
  name = "com.amazonaws.global.cloudfront.origin-facing"
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
