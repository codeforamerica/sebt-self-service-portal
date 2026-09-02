output "hostname" {
  description = "Public Keycloak base URL (https://auth.<domain>)."
  value       = local.hostname
}

output "discovery_endpoint" {
  description = "OIDC discovery document URL for the sebt realm."
  value       = "${local.hostname}/realms/sebt/.well-known/openid-configuration"
}

output "authorization_endpoint" {
  description = "OIDC authorization endpoint for the sebt realm."
  value       = "${local.hostname}/realms/sebt/protocol/openid-connect/auth"
}

output "endpoint_url" {
  description = "FQDN created for the Keycloak ALB (without scheme)."
  value       = module.service.endpoint_url
}

output "security_group_id" {
  description = "Security group ID of the Keycloak ECS tasks."
  value       = module.service.security_group_id
}

output "repository_url" {
  description = "Echo of the configured image repository URL."
  value       = var.image_url
}

output "admin_secret_arn" {
  description = "Secrets Manager ARN for the Keycloak bootstrap admin credentials."
  value       = aws_secretsmanager_secret.admin.arn
  sensitive   = true
}

output "admin_bypass_secret_arn" {
  description = "Secrets Manager ARN for the ALB /admin* bypass header (JSON headerName/headerValue)."
  value       = aws_secretsmanager_secret.admin_bypass.arn
  sensitive   = true
}

output "admin_bypass_secret_name" {
  description = "Secrets Manager name for the ALB /admin* bypass header."
  value       = aws_secretsmanager_secret.admin_bypass.name
}
