output "primary_endpoint" {
  description = "Primary endpoint hostname for writes. Wire to Redis:Host and Redis:SslHost."
  value       = aws_elasticache_replication_group.main.primary_endpoint_address
}

output "reader_endpoint" {
  description = "Reader endpoint hostname, load-balanced across replicas."
  value       = aws_elasticache_replication_group.main.reader_endpoint_address
}

output "port" {
  description = "Port the cache listens on."
  value       = local.port
}

output "auth_token_secret_arn" {
  description = "ARN of the Secrets Manager secret holding the AUTH token. JSON key: auth_token."
  value       = aws_secretsmanager_secret.auth_token.arn
}

output "security_group_id" {
  description = "Security group ID for the cache."
  value       = aws_security_group.redis.id
}
