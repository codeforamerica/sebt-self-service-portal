# Data sources for use in IAM/KMS policies.
data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}

# ---------------------------------------------------------------------------
# S3 bucket for static site assets (HTML, CSS, JS)
# ---------------------------------------------------------------------------

# The bucket that stores the enrollment checker's built static files.
# It is private — only CloudFront can read from it via Origin Access Control.
resource "aws_s3_bucket" "site" {
  bucket        = "${var.project}-${var.state}-${var.environment}-enrollment-checker"
  force_destroy = var.force_delete

  tags = {
    service = "enrollment-checker"
  }
}

# Block all public access. Even if someone misconfigures a bucket policy,
# these settings act as a safety net to prevent public exposure.
resource "aws_s3_bucket_public_access_block" "site" {
  bucket = aws_s3_bucket.site.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Send S3 access logs to the shared logging bucket.
resource "aws_s3_bucket_logging" "site" {
  bucket        = aws_s3_bucket.site.id
  target_bucket = var.logging_bucket_domain_name
  target_prefix = "s3/enrollment-checker/"
}

# Keep previous versions of files so we can roll back a bad deploy.
resource "aws_s3_bucket_versioning" "site" {
  bucket = aws_s3_bucket.site.id

  versioning_configuration {
    status = "Enabled"
  }
}

# Dedicated KMS key for the site bucket. CloudFront needs decrypt permission
# to serve objects encrypted with KMS, and the shared logging key's policy
# doesn't grant that — so we create a separate key with the right policy.
resource "aws_kms_key" "site" {
  description             = "Encryption key for the ${var.project}-${var.state}-${var.environment} enrollment checker bucket."
  deletion_window_in_days = 7
  enable_key_rotation     = true
  policy = jsonencode(yamldecode(templatefile("${path.module}/templates/bucket-key-policy.yaml.tftpl", {
    account_id = data.aws_caller_identity.current.account_id
    partition  = data.aws_partition.current.partition
    # CloudFront distribution ARN is needed here but the distribution is
    # created in a later step. We use a wildcard for now and will tighten
    # this once the distribution resource exists.
    distribution_arn = "*"
  })))

  tags = {
    service = "enrollment-checker"
  }
}

resource "aws_kms_alias" "site" {
  name          = "alias/${var.project}/${var.state}/${var.environment}/enrollment-checker"
  target_key_id = aws_kms_key.site.id
}

# Encrypt all objects at rest using the dedicated KMS key.
resource "aws_s3_bucket_server_side_encryption_configuration" "site" {
  bucket = aws_s3_bucket.site.id

  rule {
    bucket_key_enabled = true

    apply_server_side_encryption_by_default {
      kms_master_key_id = aws_kms_key.site.arn
      sse_algorithm     = "aws:kms"
    }
  }
}

# Lifecycle rules to control storage costs:
# - Delete old file versions after 90 days
# - Clean up incomplete multipart uploads after 7 days
resource "aws_s3_bucket_lifecycle_configuration" "site" {
  bucket = aws_s3_bucket.site.id

  rule {
    id     = "expire-noncurrent-versions"
    status = "Enabled"

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }

  rule {
    id     = "abort-incomplete-multipart-uploads"
    status = "Enabled"

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}
