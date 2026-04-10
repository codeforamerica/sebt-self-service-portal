variable "domain" {
  type        = string
  description = "Fully qualified domain name for the enrollment checker (e.g. dev.co.sebt-enrollment.codeforamerica.app)."
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. development, production)."
}

variable "force_delete" {
  type        = bool
  description = "Allow destruction of the S3 bucket even if it contains objects."
  default     = false
}

variable "hosted_zone_id" {
  type        = string
  description = "Route53 hosted zone ID for DNS record creation."
}

variable "logging_bucket_domain_name" {
  type        = string
  description = "Domain name of the S3 logging bucket for access logs."
}

variable "project" {
  type        = string
  description = "Project name used for resource naming."
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. co, dc)."
}
