variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "development"
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

variable "project" {
  type        = string
  description = "Project name used for resource naming."
  default     = "sebt-portal"
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. co, dc)."
  default     = "dc"
}

variable "vpc_cidr" {
  type        = string
  description = "IPv4 CIDR block for the VPC."
}

variable "amplitude_api_key" {
  type        = string
  description = "Amplitude browser SDK key served to the web tier at request time. Empty leaves Amplitude off."
  default     = ""
}

variable "mixpanel_token" {
  type        = string
  description = "Mixpanel project token served to the web tier at request time. Empty leaves Mixpanel off."
  default     = ""
}

variable "siteimprove_id" {
  type        = string
  description = "SiteImprove site ID served to the web tier at request time. Empty leaves SiteImprove off."
  default     = ""
}

variable "smarty_embedded_key" {
  type        = string
  description = "Smarty embeddable key for address type-ahead, served to the web tier at request time. Empty disables autocomplete."
  default     = ""
}

variable "socure_di_sdk_key" {
  type        = string
  description = "Socure Device Intelligence SDK key served to the web tier at request time. Empty leaves Device Intelligence off."
  default     = ""
}
