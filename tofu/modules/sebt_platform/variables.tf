variable "domain" {
  type        = string
  description = "Domain of the account's primary deployment. Tags the WAF log group."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
}

variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet CIDR blocks."
}

variable "project" {
  type        = string
  description = "Project name used for resource naming."
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet CIDR blocks."
}

variable "state" {
  type        = string
  description = "State whose AWS account these resources live in. Resource names are built from it."
}

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}
