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
