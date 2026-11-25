output "database" {
  description = "RDS SQL Server instance, in the shape sebt_application's database input takes."
  value = {
    host              = data.aws_db_instance.main.address
    port              = data.aws_db_instance.main.port
    admin_secret_arn  = data.aws_db_instance.main.master_user_secret[0].secret_arn
    security_group_id = data.aws_security_group.database.id
  }
}

output "logging_bucket" {
  description = "S3 bucket used to store logs."
  value       = data.aws_s3_bucket.logging.id
}

output "logging_bucket_domain_name" {
  description = "Domain name of the S3 bucket used to store logs."
  value       = data.aws_s3_bucket.logging.bucket_domain_name
}

output "logging_key_arn" {
  description = "ARN of the KMS key used to encrypt logs."
  value       = data.aws_kms_alias.logging.target_key_arn
}

output "name_prefix" {
  description = "Prefix the platform's resources are named with: project-state-environment."
  value       = local.prefix
}

output "private_subnets" {
  description = "IDs of the VPC's private subnets."
  value       = sort(data.aws_subnets.private.ids)
}

output "public_subnets" {
  description = "IDs of the VPC's public subnets."
  value       = sort(data.aws_subnets.public.ids)
}

output "vpc_id" {
  description = "ID of the VPC."
  value       = data.aws_vpc.main.id
}

output "waf_log_group_arn" {
  description = "ARN of the CloudWatch log group WAF logs are sent to."
  value       = data.aws_cloudwatch_log_group.waf.arn
}
