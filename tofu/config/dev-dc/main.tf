terraform {
  backend "s3" {
    bucket         = "sebt-portal-dev-tfstate"
    key            = "dev-dc/backend.tfstate"
    dynamodb_table = "dev.tfstate"
    region         = "us-east-1"
  }
}

# Create an S3 bucket and KMS key for logging.                                                                                                    
module "logging" {
  source = "github.com/codeforamerica/tofu-modules-aws-logging?ref=2.1.2"

  project     = "sebt-portal"
  environment = "dev"

  log_groups_to_datadog = false
}

# Create a VPC with public and private subnets. Since this is a dev
# environment, we'll use a single NAT gateway to reduce costs.
module "vpc" {
  source = "github.com/codeforamerica/tofu-modules-aws-vpc?ref=1.1.2"

  project            = "sebt-portal"
  environment        = "dev"
  single_nat_gateway = true
  logging_key_id     = module.logging.kms_key_arn

  cidr            = var.vpc_cidr
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets
}
