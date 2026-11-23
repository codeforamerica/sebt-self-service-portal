terraform {
  backend "s3" {
    bucket         = "sebt-portal-co-development-tfstate"
    key            = "dev-co-shared/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Resources shared by every application deployment in the CO development
# account (dev-co and dev-demo). Apply this before those configs; they find
# these resources with the sebt_platform_lookup module.
module "platform" {
  source = "../../modules/sebt_platform"

  project         = var.project
  state           = var.state
  environment     = var.environment
  domain          = var.domain
  vpc_cidr        = var.vpc_cidr
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets
}

# Look up the Keycloak ECR repository created by dev-co's bootstrap.
data "aws_ecr_repository" "keycloak" {
  name = "${var.project}-${var.state}-${var.environment}-keycloak"
}

data "aws_route53_zone" "main" {
  name = "co.sebt-portal.codeforamerica.app"
}

# Shared Keycloak IdP for the non-production OIDC deployments in this account.
# Push an image before the first apply: ./scripts/preview/build-keycloak.sh
module "keycloak" {
  source = "../../modules/sebt_keycloak"

  project             = var.project
  state               = var.state
  environment         = var.environment
  domain              = var.domain
  hosted_zone_id      = data.aws_route53_zone.main.zone_id
  vpc_id              = module.platform.vpc_id
  private_subnets     = module.platform.private_subnets
  public_subnets      = module.platform.public_subnets
  logging_key_id      = module.platform.logging_key_arn
  image_url           = data.aws_ecr_repository.keycloak.repository_url
  repository_arn      = data.aws_ecr_repository.keycloak.arn
  image_tag           = var.keycloak_image_tag
  force_delete        = true
  skip_final_snapshot = true
  admin_ingress_cidrs = var.keycloak_admin_ingress_cidrs
}
