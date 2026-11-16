output "app_user_secret_arn" {
  description = "ARN of the Secrets Manager secret containing the active app-user DB credentials."
  value       = aws_secretsmanager_secret.app_user.arn
}
