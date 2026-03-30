data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}
data "aws_region" "current" {}

data "archive_file" "rotation_lambda" {
  type        = "zip"
  source_file = "${path.module}/lambda/rotate_smtp_credentials.py"
  output_path = "${path.module}/lambda/rotate_smtp_credentials.zip"
}
