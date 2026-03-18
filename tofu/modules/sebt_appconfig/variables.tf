variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}
