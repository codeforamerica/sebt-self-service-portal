output "kms_key" {
  description = "KMS key used to encrypt state."
  value       = module.backend.kms_key
}

output "tfstate_bucket" {
  description = "S3 bucket name for OpenTofu remote state."
  value       = module.backend.bucket
}
