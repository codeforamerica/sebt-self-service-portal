output "tfstate_bucket" {
  description = "S3 bucket name for OpenTofu/Terraform remote state."
  value       = aws_s3_bucket.tfstate.bucket
}

output "tflock_table" {
  description = "DynamoDB table name for state locking."
  value       = aws_dynamodb_table.tflock.name
}
