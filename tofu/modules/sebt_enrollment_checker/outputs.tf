output "s3_bucket_id" {
  description = "ID of the S3 bucket for the enrollment checker static site."
  value       = aws_s3_bucket.site.id
}

output "s3_bucket_arn" {
  description = "ARN of the S3 bucket."
  value       = aws_s3_bucket.site.arn
}

output "s3_bucket_regional_domain_name" {
  description = "Regional domain name of the S3 bucket (used as CloudFront origin)."
  value       = aws_s3_bucket.site.bucket_regional_domain_name
}

output "cloudfront_distribution_id" {
  description = "ID of the CloudFront distribution (used for cache invalidation in CI/CD)."
  value       = aws_cloudfront_distribution.site.id
}

output "cloudfront_distribution_domain" {
  description = "Domain name of the CloudFront distribution."
  value       = aws_cloudfront_distribution.site.domain_name
}

output "site_url" {
  description = "Public URL of the enrollment checker."
  value       = "https://${var.domain}"
}
