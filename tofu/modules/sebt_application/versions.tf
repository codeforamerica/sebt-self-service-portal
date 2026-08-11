terraform {
  required_version = ">= 1.6.0"

  required_providers {
    doppler = {
      source  = "dopplerhq/doppler"
      version = "~> 1.20"
    }
  }
}
