variable "domain" {
  type        = string
  description = "Domain of the CO development deployment; Keycloak is served at auth.<domain>."
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "development"
}

variable "keycloak_admin_ingress_cidrs" {
  type        = list(string)
  description = <<-EOT
    Optional source CIDRs allowed to reach Keycloak /admin* (console and Admin
    REST API) without the ALB bypass header. Leave empty to rely on the header
    secret used by preview deploy scripts. Set via TF_VAR_keycloak_admin_ingress_cidrs
    when office/VPN break-glass is needed.
  EOT
  default     = []
}

variable "keycloak_image_tag" {
  type        = string
  description = "Image tag for the shared Keycloak service."
  default     = "latest"
}

variable "private_subnets" {
  type        = list(string)
  description = "List of private subnet CIDR blocks."
}

variable "project" {
  type        = string
  description = "Project name used for resource naming."
  default     = "sebt-portal"
}

variable "public_subnets" {
  type        = list(string)
  description = "List of public subnet CIDR blocks."
}

variable "state" {
  type        = string
  description = "State whose AWS account these resources live in. Resource names are built from it."
  default     = "co"
}

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}
