output "appconfig_application_id" {
  description = "ID of the AppConfig application."
  value       = var.enable_appconfig ? module.appconfig[0].application_id : null
}

output "api_cluster_name" {
  description = "ECS cluster name running the API service. Exposed so DC-only seed task can run on the same cluster."
  value       = module.api.cluster_name
}

output "api_endpoint_url" {
  description = "URL of the API service endpoint."
  value       = module.api.endpoint_url
}

output "api_repository_url" {
  description = "ECR repository URL for the API service."
  value       = module.api.repository_url
}

output "api_security_group_id" {
  description = "Security group ID of the API service."
  value       = module.api.security_group_id
}

output "database_endpoint" {
  description = "RDS SQL Server endpoint."
  value       = module.database.endpoint
}

output "database_secret_arn" {
  description = "ARN of the Secrets Manager secret containing database credentials."
  value       = module.database.secret_arn
}

output "ses_secret_arn" {
  description = "ARN of the Secrets Manager secret containing SES SMTP credentials."
  value       = module.ses.secret_arn
}

output "web_endpoint_url" {
  description = "URL of the Web service endpoint."
  value       = module.web.endpoint_url
}

output "web_repository_url" {
  description = "ECR repository URL for the Web service."
  value       = module.web.repository_url
}
