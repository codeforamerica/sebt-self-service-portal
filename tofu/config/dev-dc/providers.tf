provider "aws" {
  region = "us-east-1"

  default_tags {
    tags = {
      project     = "sebt-portal"
      environment = "dev"
      state       = "dc"
      tofu        = "true"
    }
  }
}
