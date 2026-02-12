resource "aws_ses_email_identity" "sender" {
  email = var.sender_email
}

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

resource "aws_iam_access_key" "smtp" {
  user = aws_iam_user.smtp.name
}

resource "aws_secretsmanager_secret" "smtp" {
  name        = "${local.prefix}-ses-smtp-credentials"
  description = "SES SMTP credentials for ${local.prefix}."

  tags = {
    Name = "${local.prefix}-ses-smtp-credentials"
  }
}

resource "aws_secretsmanager_secret_version" "smtp" {
  secret_id = aws_secretsmanager_secret.smtp.id

  secret_string = jsonencode({
    username = aws_iam_access_key.smtp.id
    password = aws_iam_access_key.smtp.ses_smtp_password_v4
  })
}

