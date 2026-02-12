variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for outgoing emails. Must be verified in SES."
}
