variable "allocated_storage" {
  type        = number
  description = "Allocated storage in GB."
  default     = 20
}

variable "apply_immediately" {
  type        = bool
  description = "Apply changes immediately rather than during the next maintenance window."
  default     = false
}

variable "backup_retention_period" {
  type        = number
  description = "Number of days to retain backups."
  default     = 7

  validation {
    condition     = var.backup_retention_period >= 1 && var.backup_retention_period <= 35
    error_message = "backup_retention_period must be between 1 and 35 days."
  }
}

variable "engine" {
  type        = string
  description = "SQL Server engine type."
  default     = "sqlserver-ex"

  validation {
    condition     = contains(["sqlserver-ex", "sqlserver-se"], var.engine)
    error_message = "engine must be either 'sqlserver-ex' (Express) or 'sqlserver-se' (Standard)."
  }
}

variable "engine_version" {
  type        = string
  description = "SQL Server engine version."
  default     = "16.00.4215.2.v1"
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "ingress_cidrs" {
  type        = list(string)
  description = "CIDR blocks allowed to connect to the database."
  default     = []
}

variable "ingress_security_groups" {
  type        = list(string)
  description = "Security group IDs allowed to connect to the database."
}

variable "instance_class" {
  type        = string
  description = "RDS instance class."
  default     = "db.t3.micro"
}

variable "key_recovery_period" {
  type        = number
  description = "Number of days before a KMS key is deleted after destruction."
  default     = 30

  validation {
    condition     = var.key_recovery_period >= 7 && var.key_recovery_period <= 30
    error_message = "key_recovery_period must be between 7 and 30 days."
  }
}

variable "logging_key_arn" {
  type        = string
  description = "KMS key ARN for encrypting CloudWatch logs."
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "project_short" {
  type        = string
  description = "Abbreviated project name for resource naming."
  default     = ""
}

variable "skip_final_snapshot" {
  type        = bool
  description = "Skip final snapshot when destroying the database."
  default     = false
}

variable "subnets" {
  type        = list(string)
  description = "List of subnet IDs for the database."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID where the database will be created."
}
