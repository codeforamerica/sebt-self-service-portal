
variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "development"
}

variable "project" {
  type        = string
  description = "Project that these resources are supporting."
  default     = "sebt-portal"
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. dc, co)."
  default     = "co"
}
