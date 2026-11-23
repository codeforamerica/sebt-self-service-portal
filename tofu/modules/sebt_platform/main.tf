# Sized for development accounts: one NAT gateway, no final DB snapshot, changes applied immediately.

# Create an S3 bucket and KMS key for logging.
module "logging" {
  source = "github.com/codeforamerica/tofu-modules-aws-logging?ref=2.2.0"

  project     = "${var.project}-${var.state}"
  environment = var.environment
  log_groups = {
    "waf" = {
      name = "aws-waf-logs-cfa/${var.project}-${var.state}/${var.environment}"
      tags = {
        source = "waf"
        webacl = "${var.project}-${var.state}-${var.environment}"
        domain = var.domain
      }
    }
  }

  log_groups_to_datadog = true
}

# Create a VPC with public and private subnets. Since this is a dev
# environment, we'll use a single NAT gateway to reduce costs.
module "vpc" {
  source = "github.com/codeforamerica/tofu-modules-aws-vpc?ref=1.2.1"

  project            = "${var.project}-${var.state}"
  environment        = var.environment
  single_nat_gateway = true
  logging_key_id     = module.logging.kms_key_arn

  cidr            = var.vpc_cidr
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets
}

# SSM bastion for developer DB access. Uses pure-SSM port forwarding;
# no PEM distribution, no SSH. Access is IAM-gated via SSO.
module "bastion" {
  source = "github.com/codeforamerica/tofu-modules-aws-ssm-bastion?ref=1.1.0"

  project                 = "${var.project}-${var.state}"
  environment             = var.environment
  private_subnet_ids      = module.vpc.private_subnets
  vpc_id                  = module.vpc.vpc_id
  kms_key_recovery_period = 7
  instance_profile        = null
}

# RDS SQL Server instance. Each application deployment creates its own login
# and database on it.
module "database" {
  source = "../sebt_database"

  project = "${var.project}-${var.state}"
  # sebt_application's default; the existing security group's name is built from it.
  project_short   = "sebt"
  environment     = var.environment
  vpc_id          = module.vpc.vpc_id
  subnets         = module.vpc.private_subnets
  logging_key_arn = module.logging.kms_key_arn

  ingress_security_groups = []
  # Every deployment's API and the SSM bastion connect from inside the VPC.
  ingress_cidrs = [var.vpc_cidr]

  skip_final_snapshot = true
  apply_immediately   = true
}
