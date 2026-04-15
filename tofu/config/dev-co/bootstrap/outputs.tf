output "kms_key" {
  description = "KMS key used to encrypt state."
  value       = module.backend.kms_key
}

output "tfstate_bucket" {
  description = "S3 bucket name for OpenTofu remote state."
  value       = module.backend.bucket
}

output "github_actions_access_key_id" {
  description = "Access key ID for the GitHub Actions IAM user."
  value       = aws_iam_access_key.github_actions.id
}

output "github_actions_secret_access_key" {
  description = "Secret access key for the GitHub Actions IAM user."
  value       = aws_iam_access_key.github_actions.secret
  sensitive   = true
}

output "enrollment_checker_nameservers" {
  description = "NS records for the enrollment checker hosted zone. Send these to the CfA DNS maintainer for delegation."
  value       = aws_route53_zone.enrollment_checker.name_servers
}

output "ecr_api_repository_url" {
  description = "ECR repository URL for the API image."
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_web_repository_url" {
  description = "ECR repository URL for the web image."
  value       = aws_ecr_repository.web.repository_url
}
