variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "project_short" {
  type        = string
  description = "Abbreviated project name for resource naming."
  default     = ""
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for outgoing emails. Must be verified in SES."
}

