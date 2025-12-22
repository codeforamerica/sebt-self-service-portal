output "alb_dns_name" {
  description = "Public DNS name for the load balancer."
  value       = aws_lb.main.dns_name
}

output "ecr_api_repository_url" {
  description = "ECR repository URL for the API image."
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_web_repository_url" {
  description = "ECR repository URL for the Web image."
  value       = aws_ecr_repository.web.repository_url
}

output "deployed_api_image" {
  description = "Full image URI deployed for the API task definition."
  value       = local.api_image
}

output "deployed_web_image" {
  description = "Full image URI deployed for the Web task definition."
  value       = local.web_image
}

output "github_actions_ecr_push_role_arn" {
  description = "IAM role ARN for GitHub Actions to push to ECR (if enabled)."
  value       = try(aws_iam_role.github_actions_ecr_push[0].arn, null)
}

output "api_url" {
  description = "API base URL (HTTP)."
  value       = "http://${aws_lb.main.dns_name}/api"
}

output "web_url" {
  description = "Web URL (HTTP)."
  value       = "http://${aws_lb.main.dns_name}/"
}

output "database_endpoint" {
  description = "RDS SQL Server endpoint."
  value       = try(aws_db_instance.main[0].endpoint, null)
}

output "database_connection_string" {
  description = "SQL Server connection string for the API."
  value       = var.enable_database ? "Server=${replace(aws_db_instance.main[0].endpoint, ":", ",")};Database=${var.database_name};User Id=${var.database_master_username};Password=${var.database_master_password};Encrypt=True;TrustServerCertificate=True;" : null
  sensitive   = true
}
