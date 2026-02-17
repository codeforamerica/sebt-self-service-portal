module "backend" {
  source = "github.com/codeforamerica/tofu-modules-aws-backend?ref=1.1.2"

  project     = "${var.project}-${var.state}" # Since bucket names are globally unique we add state to differentiate
  environment = var.environment
}

# IAM user for GitHub Actions CI/CD.
resource "aws_iam_user" "github_actions" {
  name = "${var.project}-${var.environment}-github-actions"
}

resource "aws_iam_user_policy_attachment" "github_actions" {
  user       = aws_iam_user.github_actions.name
  policy_arn = "arn:aws:iam::aws:policy/AdministratorAccess"
}

resource "aws_iam_access_key" "github_actions" {
  user = aws_iam_user.github_actions.name
}

# ECR repositories for container images.
resource "aws_ecr_repository" "api" {
  name                 = "${var.project}-${var.environment}-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "web" {
  name                 = "${var.project}-${var.environment}-web"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}
