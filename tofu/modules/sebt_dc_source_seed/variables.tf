variable "cluster_name" {
  type        = string
  description = "ECS cluster on which the seed task will run. Passed through to outputs for workflow consumption; not used by the module itself."
}

variable "db_endpoint" {
  type        = string
  description = "RDS endpoint hostname. Injected into the seed container as DB_HOST."
}

variable "db_secret_arn" {
  type        = string
  description = "ARN of the RDS-managed master credentials secret. Used to scope the IAM execution role's secretsmanager:GetSecretValue permission and to reference username/password keys in the container's secrets block."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "log_retention_days" {
  type        = number
  description = "Number of days to retain seed task logs."
  default     = 30
}

variable "logging_key_arn" {
  type        = string
  description = "KMS key ARN for CloudWatch log group encryption."
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "security_group_id" {
  type        = string
  description = "Security group ID for the seed task ENI. Reuse the API service's SG so the existing RDS ingress rule applies — no new SG or RDS ingress rule needed."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Private subnet IDs the seed task ENI attaches to. Passed through to outputs for workflow consumption."
}
