variable "project" {
  type        = string
  description = "Project name used for resource naming."
}

variable "state" {
  type        = string
  description = "State abbreviation (for example co)."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
}

variable "domain" {
  type        = string
  description = "Primary application domain. Keycloak is served at auth.<domain>."
}

variable "hosted_zone_id" {
  type        = string
  description = "Route53 hosted zone ID for the application domain."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID for Keycloak and its database."
}

variable "private_subnets" {
  type        = list(string)
  description = "Private subnet IDs for Fargate tasks and RDS."
}

variable "public_subnets" {
  type        = list(string)
  description = "Public subnet IDs for the internet-facing Keycloak ALB."
}

variable "vpc_cidr" {
  type        = string
  description = "VPC CIDR allowed to reach the Keycloak Postgres instance."
}

variable "logging_key_id" {
  type        = string
  description = "KMS key ARN/ID for CloudWatch log encryption."
}

variable "image_url" {
  type        = string
  description = "ECR repository URL for the Keycloak image (without tag)."
}

variable "repository_arn" {
  type        = string
  description = "ARN of the Keycloak ECR repository."
}

variable "image_tag" {
  type        = string
  description = "Keycloak image tag to deploy."
  default     = "latest"
}

variable "force_delete" {
  type        = bool
  description = "Allow force-delete of secrets/resources in non-prod."
  default     = true
}

variable "skip_final_snapshot" {
  type        = bool
  description = "Skip final RDS snapshot on destroy."
  default     = true
}

variable "desired_containers" {
  type        = number
  description = "Desired Keycloak task count."
  default     = 1
}
