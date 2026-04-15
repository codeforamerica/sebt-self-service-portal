output "endpoint" {
  description = "Database connection hostname."
  value       = aws_db_instance.main.address
}

output "secret_arn" {
  description = "ARN of the Secrets Manager secret containing database credentials."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "security_group_id" {
  description = "Security group ID for the database."
  value       = aws_security_group.database.id
}

output "log_group_names" {
  description = "CloudWatch log group names for the database."
  value = {
    error = aws_cloudwatch_log_group.error.name
    agent = aws_cloudwatch_log_group.agent.name
  }
}
