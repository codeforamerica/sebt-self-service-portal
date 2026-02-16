variable "api_image_url" {
  type        = string
  description = "ECR repository URL for the API image. When set, disables ECR repo creation in the fargate module."
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

variable "desired_containers" {
  type        = number
  description = "Number of desired containers for each service."
  default     = 1
}

variable "domain" {
  type        = string
  description = "Domain name for the application (e.g. dc.sebt-client-portal.dev.codeforamerica.app)."
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

variable "logging_key_id" {
  type        = string
  description = "KMS key ARN for encrypting logs."
}

variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet IDs."
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet IDs."
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for OTP emails."
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

variable "web_image_url" {
  type        = string
  description = "ECR repository URL for the web image. When set, disables ECR repo creation in the fargate module."
}

variable "web_repository_arn" {
  type        = string
  description = "ARN of the ECR repository for the web image."
}

