variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "development"
}

variable "domain" {
  type        = string
  description = "Domain name for the application."
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy."
  default     = "latest"
}

variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet CIDR blocks."
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet CIDR blocks."
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for OTP emails."
}

variable "project" {
  type        = string
  description = "Project name used for resource naming."
  default     = "sebt-portal"
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. co, dc)."
  default     = "co"
}

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}

variable "oidc_discovery_endpoint" {
  type        = string
  description = "MyColorado OIDC discovery endpoint URL."
  default     = "https://auth.pingone.com/bf29dbf3-8912-4685-a28f-bb1637f925eb/as/.well-known/openid-configuration"
}
