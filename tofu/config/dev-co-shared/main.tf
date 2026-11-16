terraform {
  backend "s3" {
    bucket         = "sebt-portal-co-development-tfstate"
    key            = "dev-co-shared/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Resources shared by every application deployment in the CO development
# account (dev-co and dev-demo). Apply this before those configs. The network
# and logging stay in dev-co and are looked up here by the names it gives them.

data "aws_vpc" "main" {
  tags = { Name = "${var.project}-${var.state}-${var.environment}" }
}

data "aws_subnets" "private" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  tags = { use = "private" }
}

data "aws_subnets" "public" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  tags = { use = "public" }
}

data "aws_kms_alias" "logging" {
  name = "alias/${var.project}-${var.state}/${var.environment}/logs"
}

# Look up the Keycloak ECR repository created by dev-co's bootstrap.
data "aws_ecr_repository" "keycloak" {
  name = "${var.project}-${var.state}-${var.environment}-keycloak"
}

data "aws_route53_zone" "main" {
  name = "co.sebt-portal.codeforamerica.app"
}

# RDS SQL Server instance. Each application deployment creates its own login
# and database on it.
module "database" {
  source = "../../modules/sebt_database"

  project = "${var.project}-${var.state}"
  # sebt_application's default; the existing security group's name is built from it.
  project_short   = "sebt"
  environment     = var.environment
  vpc_id          = data.aws_vpc.main.id
  subnets         = sort(data.aws_subnets.private.ids)
  logging_key_arn = data.aws_kms_alias.logging.target_key_arn

  ingress_security_groups = []
  # Every deployment's API and the SSM bastion connect from inside the VPC.
  ingress_cidrs = [data.aws_vpc.main.cidr_block]

  skip_final_snapshot = true
  apply_immediately   = true
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
  vpc_id              = data.aws_vpc.main.id
  private_subnets     = sort(data.aws_subnets.private.ids)
  public_subnets      = sort(data.aws_subnets.public.ids)
  logging_key_id      = data.aws_kms_alias.logging.target_key_arn
  image_url           = data.aws_ecr_repository.keycloak.repository_url
  repository_arn      = data.aws_ecr_repository.keycloak.arn
  image_tag           = var.keycloak_image_tag
  force_delete        = true
  skip_final_snapshot = true
  admin_ingress_cidrs = var.keycloak_admin_ingress_cidrs
}
