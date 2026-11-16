variable "domain" {
  type        = string
  description = "Domain name for the demo site."
  default     = "demo.sebt-portal.codeforamerica.app"
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "development"
}

variable "image_tag" {
  type        = string
  description = "Image tag to deploy for API and web."
  default     = "latest"
}

variable "keycloak_hostname" {
  type        = string
  description = "Base URL of the shared Keycloak IdP in dev-co-shared."
  default     = "https://auth.dev.co.sebt-portal.codeforamerica.app"
}

variable "project" {
  type        = string
  description = "Project name used for resource naming."
  default     = "sebt-portal"
}

variable "sender_email" {
  type        = string
  description = "Sender address for the SES identity. The demo signs in through Keycloak and sends no OTP email."
  default     = "noreply@demo.sebt-portal.codeforamerica.app"
}

variable "state" {
  type        = string
  description = "State code the demo deploys as."
  default     = "demo"
}
