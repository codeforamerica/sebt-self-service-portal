variable "aws_region" {
  type        = string
  description = "AWS region to deploy into."
}

variable "name" {
  type        = string
  description = "Base name/prefix for resources (e.g. sebt-portal)."
}

variable "stage" {
  type        = string
  description = "Environment/stage name (e.g. dev, staging, prod)."
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to all resources."
  default     = {}
}
