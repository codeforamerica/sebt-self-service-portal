terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      # Pin to major v5 to avoid surprise breaking changes.
      version = ">= 5.0, < 6.0"
    }
  }
}
