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

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source = "../../modules/sebt_application"

  domain          = var.domain
  environment     = "dev"
  image_tag       = var.image_tag
  logging_key_id  = module.logging.kms_key_arn
  private_subnets = module.vpc.private_subnets
  public_subnets  = module.vpc.public_subnets
  vpc_id          = module.vpc.vpc_id

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
}

