# Prefix lists can be used in security group rules to allow traffic from
# known AWS service IP ranges without needing to hardcode them. Here, we look
# up the managed prefix list for CloudFront origin-facing IPs so that we can
# allow them in our load balancer security rules.
# See: https://docs.aws.amazon.com/vpc/latest/userguide/working-with-aws-managed-prefix-lists.html
data "aws_ec2_managed_prefix_list" "cloudfront" {
  name = "com.amazonaws.global.cloudfront.origin-facing"
}
