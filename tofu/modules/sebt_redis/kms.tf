resource "aws_kms_key" "redis" {
  description             = "KMS key for ${local.prefix} cache at-rest and AUTH token encryption."
  deletion_window_in_days = var.key_recovery_period
  enable_key_rotation     = true

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowKeyManagement"
        Effect = "Allow"
        Principal = {
          AWS = "arn:${data.aws_partition.current.partition}:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action   = "kms:*"
        Resource = "*"
      },
      {
        Sid    = "AllowElastiCacheAccess"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:Encrypt",
          "kms:Decrypt",
          "kms:ReEncrypt*",
          "kms:GenerateDataKey*",
          "kms:CreateGrant",
          "kms:ListGrants",
          "kms:DescribeKey"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "kms:ViaService"    = "elasticache.${data.aws_region.current.name}.${data.aws_partition.current.dns_suffix}"
            "kms:CallerAccount" = data.aws_caller_identity.current.account_id
          }
        }
      },
      {
        Sid    = "AllowSecretsManagerAccess"
        Effect = "Allow"
        Principal = {
          AWS = "*"
        }
        Action = [
          "kms:Encrypt",
          "kms:Decrypt",
          "kms:ReEncrypt*",
          "kms:GenerateDataKey*",
          "kms:DescribeKey"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "kms:ViaService"    = "secretsmanager.${data.aws_region.current.name}.${data.aws_partition.current.dns_suffix}"
            "kms:CallerAccount" = data.aws_caller_identity.current.account_id
          }
        }
      },
      {
        Sid    = "AllowGrantManagement"
        Effect = "Allow"
        Principal = {
          AWS = "arn:${data.aws_partition.current.partition}:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action = [
          "kms:DescribeKey",
          "kms:GetKeyPolicy",
          "kms:ListGrants",
          "kms:RevokeGrant"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_kms_alias" "redis" {
  name          = "alias/${var.project}/${var.environment}/redis"
  target_key_id = aws_kms_key.redis.key_id
}
