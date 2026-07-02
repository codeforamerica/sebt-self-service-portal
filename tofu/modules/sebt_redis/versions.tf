terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # Valkey engine support for aws_elasticache_replication_group landed in 5.73.0.
      version = ">= 5.73"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.5"
    }
  }
}
