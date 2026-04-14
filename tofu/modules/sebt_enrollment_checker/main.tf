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
  target_bucket = var.logging_bucket_name
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
    account_id       = data.aws_caller_identity.current.account_id
    partition        = data.aws_partition.current.partition
    distribution_arn = aws_cloudfront_distribution.site.arn
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

# ---------------------------------------------------------------------------
# ACM certificate for HTTPS
# ---------------------------------------------------------------------------

# Request a TLS certificate for the enrollment checker domain. CloudFront
# requires certificates to be in us-east-1, which must be the caller's
# configured region. DNS validation proves domain ownership by checking for
# a specific CNAME record in the hosted zone.
resource "aws_acm_certificate" "site" {
  domain_name       = var.domain
  validation_method = "DNS"

  # Create the replacement certificate before destroying the old one during
  # renewal or changes, so there's no gap in coverage.
  lifecycle {
    create_before_destroy = true
  }

  tags = {
    service = "enrollment-checker"
  }
}

# Create the DNS record(s) that ACM needs to validate domain ownership.
# ACM provides specific record names and values; we just write them into
# our Route53 zone. Validation typically completes within a few minutes.
resource "aws_route53_record" "certificate_validation" {
  for_each = {
    for dvo in aws_acm_certificate.site.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = var.hosted_zone_id
}

# Wait for ACM to validate the certificate before allowing other resources
# (like the CloudFront distribution) to reference it. This prevents errors
# from trying to attach an unvalidated certificate.
resource "aws_acm_certificate_validation" "site" {
  certificate_arn         = aws_acm_certificate.site.arn
  validation_record_fqdns = [for record in aws_route53_record.certificate_validation : record.fqdn]
}

# ---------------------------------------------------------------------------
# CloudFront distribution
# ---------------------------------------------------------------------------

# Origin Access Control (OAC) lets CloudFront authenticate to S3 using
# AWS SigV4 request signing. This replaces the older Origin Access Identity
# (OAI) approach. With OAC, CloudFront signs every request to S3, and the
# bucket policy checks the signature — so the bucket never needs to be public.
resource "aws_cloudfront_origin_access_control" "site" {
  name                              = "${var.project}-${var.state}-${var.environment}-enrollment-checker"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

# The CloudFront distribution serves the static site from S3 over HTTPS.
# It acts as a CDN — caching files at edge locations close to users for
# faster delivery — and handles TLS termination using our ACM certificate.
resource "aws_cloudfront_distribution" "site" {
  aliases             = [var.domain]
  default_root_object = "index.html"
  enabled             = true
  price_class         = "PriceClass_100"

  # S3 origin: CloudFront fetches files from our private bucket using OAC.
  origin {
    domain_name              = aws_s3_bucket.site.bucket_regional_domain_name
    origin_id                = "s3"
    origin_access_control_id = aws_cloudfront_origin_access_control.site.id
  }

  # Default cache behavior: serve static files from S3.
  # GET and HEAD only — the static site has no server-side mutations.
  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true
    target_origin_id       = "s3"
    viewer_protocol_policy = "redirect-to-https"

    forwarded_values {
      query_string = false

      cookies {
        forward = "none"
      }
    }
  }

  # Handle client-side routing: when S3 returns a 404 (e.g. user navigates
  # to /check directly), serve index.html instead so the Next.js client
  # router can handle the path.
  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 10
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 10
  }

  # Send CloudFront access logs to the shared logging bucket.
  logging_config {
    bucket          = var.logging_bucket_domain_name
    include_cookies = false
    prefix          = "cloudfront/enrollment-checker/"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  # Use our ACM certificate for HTTPS. TLS 1.2 is the minimum — older
  # protocols have known vulnerabilities. SNI (Server Name Indication)
  # is the standard approach and avoids the cost of a dedicated IP.
  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate_validation.site.certificate_arn
    minimum_protocol_version = "TLSv1.2_2021"
    ssl_support_method       = "sni-only"
  }

  tags = {
    service = "enrollment-checker"
  }
}

# Bucket policy: deny non-SSL requests and allow only this CloudFront
# distribution to read objects. Applied after the distribution is created
# so we can reference its ARN.
resource "aws_s3_bucket_policy" "site" {
  bucket = aws_s3_bucket.site.id
  policy = jsonencode(yamldecode(templatefile("${path.module}/templates/bucket-policy.yaml.tftpl", {
    bucket_arn       = aws_s3_bucket.site.arn
    distribution_arn = aws_cloudfront_distribution.site.arn
  })))

  depends_on = [aws_s3_bucket_public_access_block.site]
}

# Route53 A record pointing the enrollment checker domain at CloudFront.
# This is an "alias" record — a Route53-specific feature that maps a
# domain directly to an AWS resource without a CNAME. It works at the
# zone apex and has no TTL (queries resolve instantly via Route53).
resource "aws_route53_record" "site" {
  name    = var.domain
  type    = "A"
  zone_id = var.hosted_zone_id

  alias {
    evaluate_target_health = false
    name                   = aws_cloudfront_distribution.site.domain_name
    zone_id                = aws_cloudfront_distribution.site.hosted_zone_id
  }
}
