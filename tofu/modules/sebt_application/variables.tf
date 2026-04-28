variable "api_cpu" {
  type        = number
  description = "CPU units for the API service container."
  default     = 512
}

variable "api_image_url" {
  type        = string
  description = "ECR repository URL for the API image. When set, disables ECR repo creation in the fargate module."
}

variable "api_memory" {
  type        = number
  description = "Memory (in MiB) for the API service container."
  default     = 1024
}

variable "api_repository_arn" {
  type        = string
  description = "ARN of the ECR repository for the API image."
}

variable "apply_immediately" {
  type        = bool
  description = "Apply database changes immediately rather than during the next maintenance window."
  default     = false
}

variable "db_ingress_cidrs" {
  type        = list(string)
  description = "Extra CIDR blocks allowed to connect to the database on TCP 1433. Used to grant VPC-internal clients (e.g. the SSM bastion) access without plumbing security groups."
  default     = []
}

variable "desired_containers" {
  type        = number
  description = "Number of desired containers for each service."
  default     = 1
}

variable "domain" {
  type        = string
  description = "Domain name for the application (e.g. dc.sebt-client-portal.dev.codeforamerica.app)."
}

variable "enable_appconfig" {
  type        = bool
  description = <<-EOT
    Enable AWS AppConfig for managing feature flags and application
    settings. When enabled, creates an AppConfig application and deploys
    an AppConfig Agent sidecar alongside the API container.
    EOT
  default     = false
}

variable "enable_execute_command" {
  type        = bool
  description = "Enable ECS Exec for debugging containers."
  default     = true
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "force_delete" {
  type        = bool
  description = "Allow force deletion of resources (ECR repos, etc.)."
  default     = false
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy."
  default     = "latest"
}

variable "image_tags_mutable" {
  type        = bool
  description = "Allow mutable image tags in ECR."
  default     = false
}

variable "logging_bucket_domain_name" {
  type        = string
  description = "Domain name of the S3 bucket for logging (e.g. my-bucket.s3.amazonaws.com)."
}

variable "logging_key_id" {
  type        = string
  description = "KMS key ARN for encrypting logs."
}

variable "passive_waf" {
  type        = bool
  description = "Enable passive mode for the WAF, counting all requests rather than blocking."
  default     = false
}

variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet IDs."
}

variable "project" {
  type        = string
  description = "Base project name used for resource naming."
  default     = "sebt-portal"
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet IDs."
}

variable "rate_limit_requests" {
  type        = number
  description = "Number of requests allowed in the rate limit window. Minimum of 10, or set to 0 to disable rate limiting."
  default     = 100
}

variable "rate_limit_window" {
  type        = number
  description = "Time window, in seconds, for the rate limit. Options are: 60, 120, 300, 600"
  default     = 60
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for OTP emails."
}

variable "ses_allowed_recipients" {
  type        = list(string)
  description = "Email addresses to verify as SES recipients for sandbox testing."
  default     = []
}

variable "skip_final_snapshot" {
  type        = bool
  description = "Skip final snapshot when destroying the database."
  default     = false
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. DC, CO)."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID where resources will be created."
}

variable "waf_log_group" {
  type        = string
  description = "Name of the CloudWatch log group for WAF logs."
}

variable "web_cpu" {
  type        = number
  description = "CPU units for the web service container."
  default     = 512
}

variable "web_image_url" {
  type        = string
  description = "ECR repository URL for the web image. When set, disables ECR repo creation in the fargate module."
}

variable "web_memory" {
  type        = number
  description = "Memory (in MiB) for the web service container."
  default     = 1024
}

variable "web_repository_arn" {
  type        = string
  description = "ARN of the ECR repository for the web image."
}

variable "seeding_enabled" {
  type        = string
  description = "Enable database seeding in non-development environments."
  default     = "false"
}

variable "seeding_email_pattern" {
  type        = string
  description = "Format string for seed user emails, where {0} is the scenario name (e.g. sebt.dc+{0}@codeforamerica.org)."
  default     = ""
}

variable "use_mock_household_data" {
  type        = string
  description = "Enable mock household data seeding to create all test user scenarios."
  default     = "false"
}

variable "log_as_json" {
  type        = bool
  description = "Output API logs as structured JSON. Enable in deployed environments so Datadog can parse log severity."
  default     = false
}

variable "state_api_environment_variables" {
  type        = map(string)
  description = "State-specific environment variables to inject into the API container."
  default     = {}
}

variable "state_api_environment_secrets" {
  type        = map(string)
  description = "State-specific secrets to inject into the API container environment."
  default     = {}
}

variable "state_web_environment_variables" {
  type        = map(string)
  description = "State-specific environment variables to inject into the Web container."
  default     = {}
}

variable "state_web_environment_secrets" {
  type        = map(string)
  description = "State-specific secrets to inject into the Web container environment."
  default     = {}
}

variable "secret_recovery_period" {
  type        = number
  description = "Number of days to retain a secret before permanent deletion."
  default     = 7
}

variable "hosted_zone_id" {
  type        = string
  description = "Route 53 hosted zone ID for DNS records. Required when the zone name doesn't exactly match the domain."
  default     = ""
}

