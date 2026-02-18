variable "identifier_hasher_secret_key" {
  type        = string
  description = "Secret key for identifier hashing."
  sensitive   = true
}

variable "jwt_secret_key" {
  type        = string
  description = "Secret key for signing JWT tokens."
  sensitive   = true
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

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}
