output "secret_arn" {
  description = "ARN of the Secrets Manager secret containing SMTP credentials."
  value       = aws_secretsmanager_secret.smtp.arn
}

output "sender_email" {
  description = "Verified sender email address."
  value       = aws_ses_email_identity.sender.email
}

output "smtp_server" {
  description = "SES SMTP server endpoint."
  value       = local.smtp_server
}
