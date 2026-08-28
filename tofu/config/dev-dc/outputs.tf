output "api_endpoint_url" {
  description = "URL of the API service endpoint."
  value       = module.app.api_endpoint_url
}

output "api_repository_url" {
  description = "ECR repository URL for the API service."
  value       = module.app.api_repository_url
}

output "enrollment_checker_distribution_id" {
  description = "CloudFront distribution ID for the enrollment checker (used for cache invalidation)."
  value       = module.enrollment_checker.cloudfront_distribution_id
}

output "enrollment_checker_nameservers" {
  description = "NS records for the enrollment checker hosted zone."
  value       = data.aws_route53_zone.enrollment_checker.name_servers
}

output "enrollment_checker_s3_bucket" {
  description = "S3 bucket name for the enrollment checker static site."
  value       = module.enrollment_checker.s3_bucket_id
}

output "enrollment_checker_url" {
  description = "Public URL of the enrollment checker."
  value       = module.enrollment_checker.site_url
}

output "web_endpoint_url" {
  description = "URL of the Web service endpoint."
  value       = module.app.web_endpoint_url
}

output "web_repository_url" {
  description = "ECR repository URL for the Web service."
  value       = module.app.web_repository_url
}

output "bastion_instance_id" {
  description = "ID of the SSM bastion EC2 instance."
  value       = module.bastion.instance_id
}

output "database_endpoint" {
  description = "RDS SQL Server endpoint."
  value       = module.app.database_endpoint
}

output "database_secret_arn" {
  description = "ARN of the Secrets Manager secret with DB admin credentials."
  value       = module.app.database_secret_arn
}
