module "backend" {
  source = "github.com/codeforamerica/tofu-modules-aws-backend?ref=1.1.2"

  project     = "${var.project}-${var.state}" # Since bucket names are globally unique we add state to differentiate
  environment = var.environment
}

# IAM user for GitHub Actions CI/CD.
resource "aws_iam_user" "github_actions" {
  name = "${var.project}-${var.state}-${var.environment}-github-actions"
}

resource "aws_iam_policy" "github_actions" {
  name        = "${var.project}-${var.state}-${var.environment}-github-actions"
  description = "Service-scoped access for GitHub Actions CI/CD deployments."

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowDeploymentServices"
        Effect = "Allow"
        Action = [
          "acm:*",
          "cloudfront:*",
          "dynamodb:*",
          "ec2:*",
          "ecr:*",
          "ecs:*",
          "elasticloadbalancing:*",
          "iam:*",
          "kms:*",
          "lambda:*",
          "logs:*",
          "organizations:*",
          "rds:*",
          "route53:*",
          "s3:*",
          "secretsmanager:*",
          "ses:*",
          "ssm:*",
          "sts:*",
          "wafv2:*",
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_iam_user_policy_attachment" "github_actions" {
  user       = aws_iam_user.github_actions.name
  policy_arn = aws_iam_policy.github_actions.arn
}

resource "aws_iam_access_key" "github_actions" {
  user = aws_iam_user.github_actions.name
}

# ECR repositories for container images.
resource "aws_ecr_repository" "api" {
  name                 = "${var.project}-${var.state}-${var.environment}-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "web" {
  name                 = "${var.project}-${var.state}-${var.environment}-web"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}
