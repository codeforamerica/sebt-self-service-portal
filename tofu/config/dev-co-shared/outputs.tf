output "bastion_instance_id" {
  description = "ID of the SSM bastion EC2 instance."
  value       = module.platform.bastion_instance_id
}

output "database_endpoint" {
  description = "RDS SQL Server endpoint."
  value       = module.platform.database.host
}

output "database_secret_arn" {
  description = "ARN of the Secrets Manager secret with DB admin credentials."
  value       = module.platform.database.admin_secret_arn
}

output "keycloak_hostname" {
  description = "Public base URL for the shared Keycloak IdP."
  value       = module.keycloak.hostname
}

output "keycloak_discovery_endpoint" {
  description = "OIDC discovery URL for the shared Keycloak IdP."
  value       = module.keycloak.discovery_endpoint
}

output "keycloak_admin_secret_arn" {
  description = "Secrets Manager ARN for the shared Keycloak bootstrap admin credentials."
  value       = module.keycloak.admin_secret_arn
  sensitive   = true
}

output "keycloak_admin_bypass_secret_arn" {
  description = "Secrets Manager ARN for the ALB /admin* bypass header used by preview scripts."
  value       = module.keycloak.admin_bypass_secret_arn
  sensitive   = true
}

output "keycloak_admin_bypass_secret_name" {
  description = "Secrets Manager name for the ALB /admin* bypass header."
  value       = module.keycloak.admin_bypass_secret_name
}

output "keycloak_repository_url" {
  description = "ECR repository URL for the shared Keycloak image."
  value       = data.aws_ecr_repository.keycloak.repository_url
}
