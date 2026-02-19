variable "allowed_recipients" {
  type        = list(string)
  description = "Email addresses to verify as recipients for SES sandbox mode."
  default     = []
}

variable "domain" {
  type        = string
  description = "The domain to register with SES."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "hosted_zone_id" {
  type        = string
  description = "Route 53 hosted zone ID for DNS record creation."
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for outgoing emails."
}
