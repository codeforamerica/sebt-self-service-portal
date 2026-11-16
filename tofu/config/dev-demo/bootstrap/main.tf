# One-time setup for the demo, applied by hand. It shares dev-co's AWS account,
# state bucket, IAM user and ECR repositories, so it needs only its own zone.

# Route53 hosted zone for the demo site. Created here (not in the main config)
# because NS delegation to the parent sebt-portal.codeforamerica.app zone is an
# out-of-band step that must complete before ACM certificate validation can
# succeed.
resource "aws_route53_zone" "main" {
  name = "${var.state}.sebt-portal.codeforamerica.app"
}
