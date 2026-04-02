output "secret_arn" {
  description = "ARN of the Secrets Manager secret containing SMTP credentials."
  value       = aws_secretsmanager_secret.smtp.arn
}

output "sender_email" {
  description = "Sender email address."
  value       = var.sender_email
}

output "smtp_server" {
  description = "SES SMTP server endpoint."
  value       = local.smtp_server
}

output "rotation_lambda_arn" {
  description = "ARN of the credential rotation Lambda function."
  value       = aws_lambda_function.rotation.arn
}