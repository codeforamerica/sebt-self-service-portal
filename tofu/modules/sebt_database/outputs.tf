output "endpoint" {
  description = "Database connection endpoint."
  value       = aws_db_instance.main.endpoint
}

output "secret_arn" {
  description = "ARN of the Secrets Manager secret containing database credentials."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "security_group_id" {
  description = "Security group ID for the database."
  value       = aws_security_group.database.id
}
