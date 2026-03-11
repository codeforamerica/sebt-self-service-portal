variable "app_settings" {
  type        = map(string)
  description = "Non-flag configuration values managed via AppConfig freeform profile."
  default     = {}
}

variable "deployment_strategy" {
  type        = string
  description = <<-EOT
    Deployment strategy for AppConfig. Controls how quickly configuration
    changes roll out. Use "AllAtOnce" for immediate deployment or
    choose from options here:
    https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-creating-deployment-strategy-predefined.html.
    EOT
  default     = "AppConfig.AllAtOnce"
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "feature_flags" {
  type = map(object({
    enabled = bool
  }))
  description = <<-EOT
    Feature flags managed via AppConfig. Each key is the flag name and the
    value specifies whether the flag is enabled. Flag names should use
    snake_case to match the application convention.
    EOT
  default = {}
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
}
