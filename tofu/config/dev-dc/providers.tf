provider "aws" {
  region = "us-east-1"

  default_tags {
    tags = {
      project     = var.project
      environment = var.environment
      state       = "dc"
      tofu        = "true"
    }
  }
}
