output "api_endpoint_url" {
  description = "URL of the API service endpoint."
  value       = module.app.api_endpoint_url
}

output "web_endpoint_url" {
  description = "URL of the Web service endpoint."
  value       = module.app.web_endpoint_url
}

output "keycloak_hostname" {
  description = "Base URL of the Keycloak IdP the demo signs in through."
  value       = var.keycloak_hostname
}
