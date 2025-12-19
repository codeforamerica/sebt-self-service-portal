variable "aws_region" {
  type        = string
  description = "AWS region to deploy into."
}

variable "name" {
  type        = string
  description = "Base name/prefix for resources (e.g. sebt-portal)."
}

variable "stage" {
  type        = string
  description = "Environment/stage name (e.g. dev, staging, prod)."
}

variable "state" {
  type        = string
  description = "State deployment identifier (e.g. dc, co). Used for tagging and Next.js config."
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to all resources."
  default     = {}
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy for both API and Web (e.g. git SHA, dev, v1.2.3)."
}

variable "enable_github_actions_ecr_push" {
  type        = bool
  description = "If true, create an IAM role for GitHub Actions (OIDC) to push images to ECR."
  default     = true
}

variable "github_repo" {
  type        = string
  description = "GitHub repo in the form org/repo (used to scope the GitHub Actions OIDC role)."
  default     = "codeforamerica/sebt-self-service-portal"
}

variable "desired_count" {
  type        = number
  description = "Desired number of tasks per service."
  default     = 2
}

variable "cpu" {
  type        = number
  description = "Task CPU units (e.g. 256, 512, 1024)."
  default     = 512
}

variable "memory" {
  type        = number
  description = "Task memory (MiB) (e.g. 1024, 2048)."
  default     = 1024
}
