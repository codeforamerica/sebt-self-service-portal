terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    doppler = {
      source  = "dopplerhq/doppler"
      version = "~> 1.20"
    }
  }
}
