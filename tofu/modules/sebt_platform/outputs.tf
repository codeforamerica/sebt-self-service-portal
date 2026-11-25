output "bastion_instance_id" {
  description = "ID of the SSM bastion EC2 instance."
  value       = module.bastion.instance_id
}

output "database" {
  description = "RDS SQL Server instance, in the shape sebt_application's database input takes."
  value = {
    host              = module.database.endpoint
    port              = module.database.port
    admin_secret_arn  = module.database.secret_arn
    security_group_id = module.database.security_group_id
  }
}

output "logging_bucket" {
  description = "S3 bucket used to store logs."
  value       = module.logging.bucket
}

output "logging_bucket_domain_name" {
  description = "Domain name of the S3 bucket used to store logs."
  value       = module.logging.bucket_domain_name
}

output "logging_key_arn" {
  description = "ARN of the KMS key used to encrypt logs."
  value       = module.logging.kms_key_arn
}

output "name_prefix" {
  description = "Prefix the platform's resources are named with: project-state-environment."
  value       = local.name_prefix
}

output "private_subnets" {
  description = "IDs of the VPC's private subnets."
  value       = module.vpc.private_subnets
}

output "public_subnets" {
  description = "IDs of the VPC's public subnets."
  value       = module.vpc.public_subnets
}

output "vpc_id" {
  description = "ID of the VPC."
  value       = module.vpc.vpc_id
}

output "waf_log_group_arn" {
  description = "ARN of the CloudWatch log group WAF logs are sent to."
  value       = module.logging.log_groups["waf"]
}
