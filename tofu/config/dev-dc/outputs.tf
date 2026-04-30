output "api_endpoint_url" {
  description = "URL of the API service endpoint."
  value       = module.app.api_endpoint_url
}

output "api_repository_url" {
  description = "ECR repository URL for the API service."
  value       = module.app.api_repository_url
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
