# Domain identity and verification.
resource "aws_ses_domain_identity" "main" {
  domain = var.domain
}

resource "aws_route53_record" "ses_verification" {
  zone_id = var.hosted_zone_id
  name    = "_amazonses.${var.domain}"
  type    = "TXT"
  ttl     = 1800
  records = [aws_ses_domain_identity.main.verification_token]
}

# DKIM verification.
resource "aws_ses_domain_dkim" "main" {
  domain = aws_ses_domain_identity.main.domain
}

resource "aws_route53_record" "dkim" {
  count   = 3
  zone_id = var.hosted_zone_id
  name    = "${aws_ses_domain_dkim.main.dkim_tokens[count.index]}._domainkey.${var.domain}"
  type    = "CNAME"
  ttl     = 1800
  records = ["${aws_ses_domain_dkim.main.dkim_tokens[count.index]}.dkim.amazonses.com"]
}

# Custom MAIL FROM domain for SPF alignment.
resource "aws_ses_domain_mail_from" "main" {
  domain           = aws_ses_domain_identity.main.domain
  mail_from_domain = "bounce.${var.domain}"
}

resource "aws_route53_record" "spf_mail_from" {
  zone_id = var.hosted_zone_id
  name    = aws_ses_domain_mail_from.main.mail_from_domain
  type    = "TXT"
  ttl     = 3600
  records = ["v=spf1 include:amazonses.com -all"]
}

resource "aws_route53_record" "mx_mail_from" {
  zone_id = var.hosted_zone_id
  name    = aws_ses_domain_mail_from.main.mail_from_domain
  type    = "MX"
  ttl     = 600
  records = ["10 feedback-smtp.${data.aws_region.current.name}.amazonses.com"]
}

# DMARC policy.
resource "aws_route53_record" "dmarc" {
  zone_id = var.hosted_zone_id
  name    = "_dmarc.${var.domain}"
  type    = "TXT"
  ttl     = 3600
  records = ["v=DMARC1; p=quarantine; rua=mailto:dmarc@${var.domain};"]
}

# Sandbox recipient identities.
resource "aws_ses_email_identity" "recipients" {
  for_each = toset(var.allowed_recipients)
  email    = each.value
}

# SMTP credentials for application email sending.
resource "aws_iam_user" "smtp" {
  name = "${local.prefix}-ses-smtp"
  path = "/system/"

  tags = {
    Name = "${local.prefix}-ses-smtp"
  }
}

resource "aws_iam_user_policy" "smtp" {
  name = "ses-send"
  user = aws_iam_user.smtp.name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["ses:SendEmail", "ses:SendRawEmail"]
        Resource = "*"
      }
    ]
  })
}

resource "aws_secretsmanager_secret" "smtp" {
  name        = "${local.prefix}-ses-smtp-credentials"
  description = "SES SMTP credentials for ${local.prefix}."

  tags = {
    Name = "${local.prefix}-ses-smtp-credentials"
  }
}

# Credential rotation Lambda and supporting resources.
# Based on the AWS sample at:
# https://github.com/aws-samples/serverless-mail/tree/ses-credential-rotation/ses-credential-rotation

resource "aws_iam_role" "rotation_lambda" {
  name = "${local.prefix}-ses-smtp-rotation"
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
    Name = "${local.prefix}-ses-smtp-rotation"
  }
}

resource "aws_iam_role_policy" "rotation_lambda" {
  name = "ses-smtp-rotation"
  role = aws_iam_role.rotation_lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ManageIamKeys"
        Effect = "Allow"
        Action = [
          "iam:CreateAccessKey",
          "iam:DeleteAccessKey",
          "iam:ListAccessKeys",
        ]
        Resource = aws_iam_user.smtp.arn
      },
      {
        Sid    = "ManageSecret"
        Effect = "Allow"
        Action = [
          "secretsmanager:DescribeSecret",
          "secretsmanager:GetSecretValue",
          "secretsmanager:PutSecretValue",
          "secretsmanager:UpdateSecretVersionStage",
        ]
        Resource = aws_secretsmanager_secret.smtp.arn
      },
      {
        Sid      = "RedeployEcsService"
        Effect   = "Allow"
        Action   = "ecs:UpdateService"
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

resource "aws_cloudwatch_log_group" "rotation_lambda" {
  name              = "/aws/lambda/${local.prefix}-ses-smtp-rotation"
  retention_in_days = 30

  tags = {
    Name = "${local.prefix}-ses-smtp-rotation"
  }
}

resource "aws_lambda_function" "rotation" {
  function_name    = "${local.prefix}-ses-smtp-rotation"
  description      = "Rotates SES SMTP credentials for ${local.prefix}."
  role             = aws_iam_role.rotation_lambda.arn
  handler          = "rotate_smtp_credentials.handler"
  runtime          = "python3.12"
  timeout          = 75
  architectures    = ["arm64"]
  filename         = data.archive_file.rotation_lambda.output_path
  source_code_hash = data.archive_file.rotation_lambda.output_base64sha256

  environment {
    variables = {
      IAM_USERNAME  = aws_iam_user.smtp.name
      SMTP_ENDPOINT = local.smtp_server
      ECS_CLUSTER   = var.ecs_cluster_name
      ECS_SERVICE   = var.ecs_service_name
    }
  }

  depends_on = [aws_cloudwatch_log_group.rotation_lambda]

  tags = {
    Name = "${local.prefix}-ses-smtp-rotation"
  }
}

resource "aws_lambda_permission" "secrets_manager" {
  statement_id  = "AllowSecretsManagerInvocation"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.rotation.function_name
  principal     = "secretsmanager.amazonaws.com"
  source_arn    = aws_secretsmanager_secret.smtp.arn
}

resource "aws_secretsmanager_secret_rotation" "smtp" {
  secret_id           = aws_secretsmanager_secret.smtp.id
  rotation_lambda_arn = aws_lambda_function.rotation.arn

  rotation_rules {
    automatically_after_days = var.rotation_interval_days
  }

  # The existing SMTP credentials are still valid — just enable the schedule
  # without forcing an immediate rotation on first deploy.
  rotate_immediately = false
}
