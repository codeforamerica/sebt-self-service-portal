variable "aws_region" {
  type        = string
  description = "AWS region to deploy into."
}

variable "name" {
  type        = string
  description = "Base name/prefix for resources (e.g. sebt-portal)."
}

variable "stage" {
  type        = string
  description = "Environment/stage name (e.g. dev, staging, prod)."
}

variable "state" {
  type        = string
  description = "State deployment identifier (e.g. dc, co). Used for tagging and Next.js config."
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to all resources."
  default     = {}
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy for both API and Web (e.g. git SHA, dev, v1.2.3)."
}

variable "enable_github_actions_ecr_push" {
  type        = bool
  description = "If true, create an IAM role for GitHub Actions (OIDC) to push images to ECR."
  default     = true
}

variable "github_repo" {
  type        = string
  description = "GitHub repo in the form org/repo (used to scope the GitHub Actions OIDC role)."
  default     = "codeforamerica/sebt-self-service-portal"
}

variable "tfstate_bucket" {
  type        = string
  description = "S3 bucket used for OpenTofu state. When set (with tfstate_table), the GitHub OIDC role gets read/write access so apply-infra can run in CI."
  default     = ""
}

variable "tfstate_table" {
  type        = string
  description = "DynamoDB table used for state locking. Set with tfstate_bucket so the GitHub OIDC role can run apply-infra."
  default     = ""
}

variable "desired_count" {
  type        = number
  description = "Desired number of tasks per service."
  default     = 2
}

variable "cpu" {
  type        = number
  description = "Task CPU units (e.g. 256, 512, 1024)."
  default     = 512
}

variable "memory" {
  type        = number
  description = "Task memory (MiB) (e.g. 1024, 2048)."
  default     = 1024
}

variable "enable_database" {
  type        = bool
  description = "If true, create an RDS SQL Server instance."
  default     = false
}

variable "database_engine" {
  type        = string
  description = "SQL Server engine type: 'sqlserver-ex' (Express, supports smaller instances) or 'sqlserver-se' (Standard, requires db.t3.xlarge+)."
  default     = "sqlserver-ex"
  validation {
    condition     = contains(["sqlserver-ex", "sqlserver-se"], var.database_engine)
    error_message = "database_engine must be either 'sqlserver-ex' or 'sqlserver-se'."
  }
}

variable "database_engine_version" {
  type        = string
  description = "SQL Server engine version. For Express: '15.00.4322.2.v1' (2019) or '16.00.4215.2.v1' (2022). For Standard: '16.00.4215.2.v1' (2022, requires db.t3.xlarge+)."
  default     = "16.00.4215.2.v1" # SQL Server 2022
}

variable "database_instance_class" {
  type        = string
  description = "RDS instance class. For Express: db.t3.micro, db.t3.small, etc. For Standard: db.t3.xlarge or larger."
  default     = "db.t3.micro"
}

variable "database_allocated_storage" {
  type        = number
  description = "RDS allocated storage in GB."
  default     = 20
}

variable "database_name" {
  type        = string
  description = "Name of the database to create."
  default     = "SebtPortal"
}

variable "database_master_username" {
  type        = string
  description = "Master username for the database."
  default     = "admin"
  sensitive   = true
}

variable "database_master_password" {
  type        = string
  description = "Master password for the database."
  sensitive   = true
  default     = ""
}
