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
