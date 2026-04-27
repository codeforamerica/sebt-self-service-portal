terraform {
  backend "s3" {
    bucket         = "sebt-portal-dc-development-tfstate"
    key            = "dev-dc/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Create an S3 bucket and KMS key for logging.                                                                                                    
module "logging" {
  source = "github.com/codeforamerica/tofu-modules-aws-logging?ref=2.1.0"

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
  source = "github.com/codeforamerica/tofu-modules-aws-vpc?ref=1.1.2"

  project            = "${var.project}-${var.state}"
  environment        = var.environment
  single_nat_gateway = true
  logging_key_id     = module.logging.kms_key_arn

  cidr            = var.vpc_cidr
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets
}

# Look up ECR repositories created by bootstrap.
data "aws_ecr_repository" "api" {
  name = "${var.project}-${var.state}-${var.environment}-api"
}

data "aws_ecr_repository" "web" {
  name = "${var.project}-${var.state}-${var.environment}-web"
}

# Look up the hosted zone for DNS records.
data "aws_route53_zone" "main" {
  name = "dc.sebt-portal.codeforamerica.app"
}

# Store DC-specific secrets in Secrets Manager. Each block represents a
# separate set of secrets for a specific service or integration.
module "state_secrets" {
  source = "github.com/codeforamerica/tofu-modules-aws-secrets?ref=2.0.0"

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  secrets = {
    "socure" = {
      description     = "Socure API credentials for identity verification."
      recovery_window = 7
    }
  }
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source = "../../modules/sebt_application"

  apply_immediately          = true
  domain                     = var.domain
  hosted_zone_id             = data.aws_route53_zone.main.zone_id
  environment                = var.environment
  image_tag                  = var.image_tag
  logging_key_id             = module.logging.kms_key_arn
  logging_bucket_domain_name = module.logging.bucket_domain_name
  private_subnets            = module.vpc.private_subnets
  public_subnets             = module.vpc.public_subnets
  vpc_id                     = module.vpc.vpc_id
  db_ingress_cidrs           = [var.vpc_cidr]
  project                    = var.project
  sender_email               = var.sender_email
  skip_final_snapshot        = true
  state                      = var.state
  waf_log_group              = module.logging.log_groups["waf"]
  passive_waf                = true
  enable_appconfig           = true
  log_as_json                = true

  api_image_url      = data.aws_ecr_repository.api.repository_url
  api_repository_arn = data.aws_ecr_repository.api.arn
  web_image_url      = data.aws_ecr_repository.web.repository_url
  web_repository_arn = data.aws_ecr_repository.web.arn

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true

  seeding_enabled         = "true"
  seeding_email_pattern   = "sebt.dc+{0}@codeforamerica.org"
  use_mock_household_data = "true"

  state_api_environment_variables = {
    "IdProofingRequirements__household+view__application"        = "IAL1"
    "IdProofingRequirements__household+view__coloadedStreamline" = "IAL1"
    "IdProofingRequirements__household+view__streamline"         = "IAL1plus"
  }

  state_api_environment_secrets = {
    "Socure__ApiKey"        = "${module.state_secrets.secrets["socure"].secret_arn}:api_key"
    "Socure__WebhookSecret" = "${module.state_secrets.secrets["socure"].secret_arn}:webhook_secret"
  }

  state_web_environment_variables = {}
  state_web_environment_secrets   = {}
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
