variable "environment" {
  type        = string
  description = "Environment the platform was created for."
}

variable "project" {
  type        = string
  description = "Project name the platform's resources are named with."
}

variable "state" {
  type        = string
  description = "State whose AWS account holds the platform. Resource names are built from it."
}
