locals {
  prefix = "${var.project}-${var.environment}"
}

data "aws_region" "current" {}

data "aws_ecr_repository" "this" {
  name = "${local.prefix}-dc-source-seed"
}

resource "aws_cloudwatch_log_group" "task" {
  name              = "/ecs/${local.prefix}-dc-source-seed"
  kms_key_id        = var.logging_key_arn
  retention_in_days = var.log_retention_days

  tags = {
    Name = "${local.prefix}-dc-source-seed"
  }
}

resource "aws_iam_role" "task_execution" {
  name = "${local.prefix}-dc-source-seed-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })

  tags = {
    Name = "${local.prefix}-dc-source-seed-execution"
  }
}

resource "aws_iam_role_policy_attachment" "task_execution_managed" {
  role       = aws_iam_role.task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "task_secrets" {
  name = "${local.prefix}-dc-source-seed-secrets"
  role = aws_iam_role.task_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["secretsmanager:GetSecretValue"]
        Resource = ["${var.db_secret_arn}*"]
      },
      {
        Effect   = "Allow"
        Action   = ["kms:Decrypt"]
        Resource = "*"
        Condition = {
          StringEquals = {
            "kms:ViaService" = "secretsmanager.${data.aws_region.current.id}.amazonaws.com"
          }
        }
      }
    ]
  })
}

resource "aws_ecs_task_definition" "this" {
  family                   = "${local.prefix}-dc-source-seed"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.task_execution.arn

  container_definitions = jsonencode([
    {
      name      = "dc-source-seed"
      image     = "${data.aws_ecr_repository.this.repository_url}:latest"
      essential = true

      environment = [
        { name = "DB_HOST", value = var.db_endpoint }
      ]

      secrets = [
        { name = "DB_USER", valueFrom = "${var.db_secret_arn}:username::" },
        { name = "DB_PASSWORD", valueFrom = "${var.db_secret_arn}:password::" }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.task.name
          "awslogs-region"        = data.aws_region.current.id
          "awslogs-stream-prefix" = "seed"
        }
      }
    }
  ])

  tags = {
    Name = "${local.prefix}-dc-source-seed"
  }
}
