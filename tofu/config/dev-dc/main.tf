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
