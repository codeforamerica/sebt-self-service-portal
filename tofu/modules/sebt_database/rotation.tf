# ─── App user secret ─────────────────────────────────────────────────────────
#
# Holds the active DB credentials (either appuser or appuser_clone) that
# the API should use. The rotation Lambda alternates between the two SQL Server
# logins each cycle so that the inactive user's password can be changed while
# the active user's credentials remain valid during the ECS rolling restart.

resource "aws_secretsmanager_secret" "app_user" {
  name        = "${local.prefix}-db-app-user"
  description = "Alternating-users DB credentials for the ${local.prefix} API."

  tags = {
    Name = "${local.prefix}-db-app-user"
  }
}

resource "random_password" "app_user_initial" {
  length  = 32
  special = true
}

resource "aws_secretsmanager_secret_version" "app_user_initial" {
  secret_id = aws_secretsmanager_secret.app_user.id

  secret_string = jsonencode({
    username = "appuser"
    password = random_password.app_user_initial.result
    host     = aws_db_instance.main.address
    port     = tostring(local.port)
    dbname   = var.db_name
  })

  lifecycle {
    # Secrets Manager owns the value after the first rotation runs; ignore drift.
    ignore_changes = [secret_string]
  }
}

# ─── Lambda package ──────────────────────────────────────────────────────────
#
# pymssql ships native extensions so it must be installed for the Lambda's
# target platform (manylinux, x86_64, CPython 3.12) rather than the host OS.
# The terraform_data resource re-runs pip whenever requirements.txt changes.

resource "terraform_data" "lambda_dependencies" {
  triggers_replace = [filemd5("${path.module}/lambda/requirements.txt")]

  provisioner "local-exec" {
    command = <<-EOT
      pip install \
        --platform manylinux_2_28_x86_64 \
        --target "${path.module}/lambda" \
        --implementation cp \
        --python-version 3.12 \
        --only-binary :all: \
        --upgrade \
        --quiet \
        -r "${path.module}/lambda/requirements.txt"
    EOT
  }
}

data "archive_file" "rotation_lambda" {
  type        = "zip"
  source_dir  = "${path.module}/lambda"
  output_path = "${path.module}/rotate_db_credentials.zip"

  excludes = [
    ".venv",
    "requirements.txt",
    "requirements-test.txt",
    "test_rotate_db_credentials.py",
  ]

  depends_on = [terraform_data.lambda_dependencies]
}

# ─── Lambda security group ────────────────────────────────────────────────────

resource "aws_security_group" "rotation_lambda" {
  name_prefix = "${local.short_prefix}-db-rotation-"
  description = "DB credential rotation Lambda"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${local.prefix}-db-rotation-lambda"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# Outbound to SQL Server (for set_secret and test_secret).
resource "aws_security_group_rule" "rotation_lambda_egress_db" {
  type                     = "egress"
  protocol                 = "tcp"
  from_port                = local.port
  to_port                  = local.port
  source_security_group_id = aws_security_group.database.id
  security_group_id        = aws_security_group.rotation_lambda.id
}

# Outbound HTTPS for Secrets Manager and ECS API calls.
resource "aws_security_group_rule" "rotation_lambda_egress_https" {
  type              = "egress"
  protocol          = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.rotation_lambda.id
}

# Allow the rotation Lambda to reach the RDS instance.
resource "aws_security_group_rule" "db_ingress_rotation_lambda" {
  type                     = "ingress"
  protocol                 = "tcp"
  from_port                = local.port
  to_port                  = local.port
  source_security_group_id = aws_security_group.rotation_lambda.id
  security_group_id        = aws_security_group.database.id
}

# ─── IAM ─────────────────────────────────────────────────────────────────────

resource "aws_iam_role" "rotation_lambda" {
  name = "${local.prefix}-db-rotation"
  path = "/system/"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    Name = "${local.prefix}-db-rotation"
  }
}

# Grants the ENI permissions required for VPC-attached Lambdas.
resource "aws_iam_role_policy_attachment" "rotation_lambda_vpc_execution" {
  role       = aws_iam_role.rotation_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "rotation_lambda" {
  name = "db-rotation"
  role = aws_iam_role.rotation_lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ManageAppUserSecret"
        Effect = "Allow"
        Action = [
          "secretsmanager:DescribeSecret",
          "secretsmanager:GetSecretValue",
          "secretsmanager:PutSecretValue",
          "secretsmanager:UpdateSecretVersionStage",
        ]
        Resource = aws_secretsmanager_secret.app_user.arn
      },
      {
        Sid      = "ReadAdminSecret"
        Effect   = "Allow"
        Action   = "secretsmanager:GetSecretValue"
        Resource = aws_db_instance.main.master_user_secret[0].secret_arn
      },
      {
        Sid    = "RedeployEcsService"
        Effect = "Allow"
        Action = "ecs:UpdateService"
        Resource = "arn:${data.aws_partition.current.partition}:ecs:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:service/${var.ecs_cluster_name}/${var.ecs_service_name}"
      },
      {
        Sid    = "WriteLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents",
        ]
        Resource = "${aws_cloudwatch_log_group.rotation_lambda.arn}:*"
      },
    ]
  })
}

# ─── CloudWatch Logs ──────────────────────────────────────────────────────────

resource "aws_cloudwatch_log_group" "rotation_lambda" {
  name              = "/aws/lambda/${local.prefix}-db-rotation"
  kms_key_id        = var.logging_key_arn
  retention_in_days = 30

  tags = {
    Name = "${local.prefix}-db-rotation"
  }
}

# ─── Lambda function ──────────────────────────────────────────────────────────

resource "aws_lambda_function" "rotation" {
  function_name    = "${local.prefix}-db-rotation"
  description      = "Rotates DB credentials (alternating users) for ${local.prefix}."
  role             = aws_iam_role.rotation_lambda.arn
  handler          = "rotate_db_credentials.handler"
  runtime          = "python3.12"
  timeout          = 75
  architectures    = ["x86_64"]
  filename         = data.archive_file.rotation_lambda.output_path
  source_code_hash = data.archive_file.rotation_lambda.output_base64sha256

  vpc_config {
    subnet_ids         = var.subnets
    security_group_ids = [aws_security_group.rotation_lambda.id]
  }

  environment {
    variables = {
      ADMIN_SECRET_ARN = aws_db_instance.main.master_user_secret[0].secret_arn
      DB_HOST          = aws_db_instance.main.address
      DB_PORT          = tostring(local.port)
      DB_NAME          = var.db_name
      ECS_CLUSTER      = var.ecs_cluster_name
      ECS_SERVICE      = var.ecs_service_name
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.rotation_lambda,
    aws_iam_role_policy_attachment.rotation_lambda_vpc_execution,
  ]

  tags = {
    Name = "${local.prefix}-db-rotation"
  }
}

resource "aws_lambda_permission" "secrets_manager" {
  statement_id  = "AllowSecretsManagerInvocation"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.rotation.function_name
  principal     = "secretsmanager.amazonaws.com"
  source_arn    = aws_secretsmanager_secret.app_user.arn
}

# ─── Rotation schedule ────────────────────────────────────────────────────────

resource "aws_secretsmanager_secret_rotation" "app_user" {
  secret_id           = aws_secretsmanager_secret.app_user.id
  rotation_lambda_arn = aws_lambda_function.rotation.arn

  rotation_rules {
    automatically_after_days = var.rotation_interval_days
  }

  # Force an immediate rotation on first deploy so real credentials are
  # in place before PR 2 switches ECS to consume this secret.
  rotate_immediately = true

  depends_on = [aws_lambda_permission.secrets_manager]
}
