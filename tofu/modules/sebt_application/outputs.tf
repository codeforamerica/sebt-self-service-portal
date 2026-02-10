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
