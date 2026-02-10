terraform {
  backend "s3" {
    bucket         = "sebt-portal-dev-tfstate"
    key            = "dev-dc/backend.tfstate"
    dynamodb_table = "dev.tfstate"
    region         = "us-east-1"
  }
}
