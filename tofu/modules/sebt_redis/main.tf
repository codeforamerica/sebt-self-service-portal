resource "aws_elasticache_subnet_group" "main" {
  name       = "${local.prefix}-redis-subnet-group"
  subnet_ids = var.subnets

  tags = {
    Name = "${local.prefix}-redis-subnet-group"
  }
}

resource "aws_security_group" "redis" {
  name_prefix = "${local.short_prefix}-redis-"
  description = "ElastiCache Valkey access"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${local.prefix}-redis"
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group_rule" "this" {
  for_each = local.security_group_rules

  type                     = each.value.type
  from_port                = each.value.from_port
  to_port                  = each.value.to_port
  protocol                 = each.value.protocol
  cidr_blocks              = lookup(each.value, "cidr_blocks", null)
  source_security_group_id = lookup(each.value, "source_security_group_id", null)
  security_group_id        = aws_security_group.redis.id
}

# AUTH token (Redis password). Transit encryption must be enabled to use it.
# Alphanumeric only — ElastiCache rejects spaces and several special characters.
resource "random_password" "auth_token" {
  length  = 64
  special = false
}

resource "aws_secretsmanager_secret" "auth_token" {
  name                    = "${local.prefix}-redis-auth-token"
  description             = "ElastiCache Valkey AUTH token for ${local.prefix}."
  kms_key_id              = aws_kms_key.redis.arn
  recovery_window_in_days = var.secret_recovery_period
}

resource "aws_secretsmanager_secret_version" "auth_token" {
  secret_id     = aws_secretsmanager_secret.auth_token.id
  secret_string = jsonencode({ auth_token = random_password.auth_token.result })
}

# Replication group: cluster-mode disabled, 1 primary + N-1 replicas.
# Multi-AZ with automatic failover promotes a replica to primary on AZ loss.
resource "aws_elasticache_replication_group" "main" {
  replication_group_id = "${local.prefix}-redis"
  description          = "Valkey cache for ${local.prefix} (HybridCache L2 + distributed locking)."

  engine               = "valkey"
  engine_version       = var.engine_version
  node_type            = var.node_type
  port                 = local.port
  parameter_group_name = var.parameter_group_name

  num_cache_clusters         = var.num_cache_clusters
  automatic_failover_enabled = var.automatic_failover_enabled
  multi_az_enabled           = var.multi_az_enabled

  subnet_group_name  = aws_elasticache_subnet_group.main.name
  security_group_ids = [aws_security_group.redis.id]

  at_rest_encryption_enabled = true
  kms_key_id                 = aws_kms_key.redis.arn
  transit_encryption_enabled = true
  auth_token                 = random_password.auth_token.result
  auth_token_update_strategy = "ROTATE"

  snapshot_retention_limit = var.snapshot_retention_limit
  apply_immediately        = var.apply_immediately

  tags = {
    Name = "${local.prefix}-redis"
  }

  lifecycle {
    precondition {
      condition     = !var.automatic_failover_enabled || var.num_cache_clusters >= 2
      error_message = "automatic_failover_enabled requires num_cache_clusters >= 2."
    }

    precondition {
      condition     = !var.multi_az_enabled || var.automatic_failover_enabled
      error_message = "multi_az_enabled requires automatic_failover_enabled."
    }
  }
}
