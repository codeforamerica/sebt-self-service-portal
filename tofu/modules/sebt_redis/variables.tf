variable "apply_immediately" {
  type        = bool
  description = "Apply changes immediately rather than during the next maintenance window."
  default     = false
}

variable "automatic_failover_enabled" {
  type        = bool
  description = "Promote a read replica to primary on failure of the primary. Requires at least 2 cache clusters."
  default     = true
}

variable "engine_version" {
  type        = string
  description = "Valkey engine version. Pinned to 7.2 for parity with the redis:7 image used in local dev and tests."
  default     = "7.2"
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "ingress_cidrs" {
  type        = list(string)
  description = "CIDR blocks allowed to connect to the cache."
  default     = []
}

variable "ingress_security_groups" {
  type        = list(string)
  description = "Security group IDs allowed to connect to the cache."
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

variable "multi_az_enabled" {
  type        = bool
  description = "Place the primary and replica in different Availability Zones. Requires automatic_failover_enabled."
  default     = true
}

variable "node_type" {
  type        = string
  description = "ElastiCache node instance type."
  default     = "cache.t4g.micro"
}

variable "num_cache_clusters" {
  type        = number
  description = "Number of nodes in the replication group (1 primary + N-1 replicas). Use 2 for a primary/replica pair."
  default     = 2

  validation {
    condition     = var.num_cache_clusters >= 1 && var.num_cache_clusters <= 6
    error_message = "num_cache_clusters must be between 1 and 6."
  }
}

variable "parameter_group_name" {
  type        = string
  description = "ElastiCache parameter group. Must match the engine major version family."
  default     = "default.valkey7"
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

variable "secret_recovery_period" {
  type        = number
  description = "Number of days before the AUTH token secret is permanently deleted after destruction. 0 forces immediate deletion (dev-friendly)."
  default     = 0
}

variable "snapshot_retention_limit" {
  type        = number
  description = "Days to retain automatic snapshots. 0 disables snapshots — appropriate for ephemeral session/cache data."
  default     = 0
}

variable "subnets" {
  type        = list(string)
  description = "List of private subnet IDs for the cache subnet group."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID where the cache will be created."
}
