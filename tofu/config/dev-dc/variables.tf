variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet CIDR blocks."
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet CIDR blocks."
}

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}
