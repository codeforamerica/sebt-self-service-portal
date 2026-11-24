# Look up the Keycloak ECR repository created by dev-co's bootstrap.
data "aws_ecr_repository" "keycloak" {
  name = "${var.project}-${var.state}-${var.environment}-keycloak"
}

data "aws_route53_zone" "main" {
  name = "co.sebt-portal.codeforamerica.app"
}
