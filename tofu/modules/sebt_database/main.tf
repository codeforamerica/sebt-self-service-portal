resource "aws_db_subnet_group" "main" {
  name       = "${local.prefix}-db-subnet-group"
  subnet_ids = var.subnets

  tags = {
    Name = "${local.prefix}-db-subnet-group"
  }
}

resource "aws_security_group" "database" {
  name_prefix = "${local.short_prefix}-db-"
  description = "RDS SQL Server access"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${local.prefix}-database"
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
  security_group_id        = aws_security_group.database.id
}

resource "aws_cloudwatch_log_group" "error" {
  name              = "/aws/rds/instance/${local.prefix}-db/error"
  kms_key_id        = var.logging_key_arn
  retention_in_days = 30

  tags = {
    Name = "${local.prefix}-db-error-logs"
  }
}

resource "aws_cloudwatch_log_group" "agent" {
  name              = "/aws/rds/instance/${local.prefix}-db/agent"
  kms_key_id        = var.logging_key_arn
  retention_in_days = 30

  tags = {
    Name = "${local.prefix}-db-agent-logs"
  }
}

resource "aws_db_instance" "main" {
  identifier = "${local.prefix}-db"

  engine         = var.engine
  engine_version = data.aws_rds_engine_version.this.version
  license_model  = var.engine == "sqlserver-se" ? "license-included" : null
  instance_class = var.instance_class

  allocated_storage     = var.allocated_storage
  max_allocated_storage = var.allocated_storage * 2
  storage_type          = "gp3"
  storage_encrypted     = true
  kms_key_id            = aws_kms_key.database.arn

  manage_master_user_password   = true
  master_user_secret_kms_key_id = aws_kms_key.database.arn
  username                      = "admin"

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.database.id]
  publicly_accessible    = false

  backup_retention_period = var.backup_retention_period

  apply_immediately         = var.apply_immediately
  skip_final_snapshot       = var.skip_final_snapshot
  final_snapshot_identifier = var.skip_final_snapshot ? null : "${local.prefix}-db-final-snapshot"

  enabled_cloudwatch_logs_exports = ["error", "agent"]
  performance_insights_enabled    = var.environment != "dev"

  tags = {
    Name = "${local.prefix}-database"
  }
}
