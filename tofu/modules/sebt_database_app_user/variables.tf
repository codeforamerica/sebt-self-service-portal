variable "additional_db_names" {
  type        = list(string)
  description = "Additional databases on this RDS instance (beyond db_name) where the app-user login also needs a database-level user provisioned, e.g. DC's DcSource database. Empty means only db_name is provisioned — the typical case for CO, which has no equivalent database."
  default     = []
}

variable "admin_secret_arn" {
  type        = string
  description = "ARN of the RDS-managed master credentials secret, used by the rotation Lambda to create and update the app-user logins."
}

variable "app_users" {
  type        = list(string)
  description = "Pair of SQL Server logins that alternate as the active credential. Deployments sharing an RDS instance need distinct pairs, since logins are server-wide."
  default     = ["appuser", "appuser_clone"]

  validation {
    condition     = length(var.app_users) == 2
    error_message = "app_users must name exactly two logins."
  }
}

variable "database_security_group_id" {
  type        = string
  description = "Security group of the RDS instance. The rotation Lambda is granted ingress to it."
}

variable "db_name" {
  type        = string
  description = "Database name used for app-user creation and connection tests."
  default     = "SebtPortal"
}

variable "ecs_cluster_name" {
  type        = string
  description = "Name of the ECS cluster running the API service (for redeployment on credential rotation)."
}

variable "ecs_service_name" {
  type        = string
  description = "Name of the ECS service to redeploy when DB credentials are rotated."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
}

variable "host" {
  type        = string
  description = "Hostname of the RDS SQL Server instance."
}

variable "logging_key_arn" {
  type        = string
  description = "KMS key ARN for encrypting CloudWatch logs."
}

variable "port" {
  type        = number
  description = "Port the RDS SQL Server instance listens on."
  default     = 1433
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "project_short" {
  type        = string
  description = "Abbreviated project name for resource naming."
  default     = null
}

variable "rotation_interval_days" {
  type        = number
  description = "Number of days between automatic DB credential rotations."
  default     = 30
}

variable "subnets" {
  type        = list(string)
  description = "List of subnet IDs for the rotation Lambda."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID where the rotation Lambda runs."
}
