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

variable "vpc_id" {
  type        = string
  description = "VPC ID where resources will be created."
}

