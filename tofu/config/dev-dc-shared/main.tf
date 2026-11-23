terraform {
  backend "s3" {
    bucket         = "sebt-portal-dc-development-tfstate"
    key            = "dev-dc-shared/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Resources shared by every application deployment in the DC development
# account. Apply this before dev-dc, which finds these resources with the
# sebt_platform_lookup module.
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
