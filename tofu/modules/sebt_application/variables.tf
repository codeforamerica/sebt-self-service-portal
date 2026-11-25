variable "api_cpu" {
  type        = number
  description = "CPU units for the API service container."
  default     = 512
}

variable "api_memory" {
  type        = number
  description = "Memory (in MiB) for the API service container."
  default     = 1024
}

variable "apply_immediately" {
  type        = bool
  description = "Apply database changes immediately rather than during the next maintenance window."
  default     = false
}

variable "db_app_users" {
  type        = list(string)
  description = "Pair of SQL Server logins that alternate as the API's credential. Must differ from every other deployment on the same RDS instance."
  default     = ["appuser", "appuser_clone"]
}

variable "db_name" {
  type        = string
  description = "Name of the API's database on the RDS instance. Created on first credential rotation if missing."
  default     = "SebtPortal"
}

variable "desired_containers" {
  type        = number
  description = "Number of desired containers for each service."
  default     = 1
}

variable "domain" {
  type        = string
  description = "Domain name for the application (e.g. dc.sebt-client-portal.dev.codeforamerica.app)."
}

variable "certificate_sans" {
  type        = list(string)
  description = <<-EOT
    Additional ACM subject alternative names for the API and public Web
    certificates. Pass wildcard names such as `["*.dev.co.example.app"]` so
    ephemeral preview hosts like `pr-N` / `api-pr-N` can terminate TLS on the
    shared ALBs. Defaults to none.
    EOT
  default     = []
}

variable "cloudfront_extra_aliases" {
  type        = list(string)
  description = <<-EOT
    Additional CloudFront alternate domain names beyond the primary site
    domain. Pass the same wildcard used in `certificate_sans` (such as
    `["*.dev.co.example.app"]`) so preview hosts can be served publicly
    through CloudFront. Defaults to none.
    EOT
  default     = []
}

variable "enable_appconfig" {
  type        = bool
  description = <<-EOT
    Enable AWS AppConfig for managing feature flags and application
    settings. When enabled, creates an AppConfig application and deploys
    an AppConfig Agent sidecar alongside the API container.
    EOT
  default     = false
}

variable "enable_execute_command" {
  type        = bool
  description = "Enable ECS Exec for debugging containers."
  default     = true
}

variable "environment" {
  type        = string
  description = "Environment for the deployment."
  default     = "dev"
}

variable "force_delete" {
  type        = bool
  description = "Allow force deletion of resources (ECR repos, etc.)."
  default     = false
}

variable "hosted_zone_name" {
  type        = string
  description = "Name of the Route 53 hosted zone for DNS records (e.g. co.sebt-portal.codeforamerica.app)."
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy."
  default     = "latest"
}

variable "image_tags_mutable" {
  type        = bool
  description = "Allow mutable image tags in ECR."
  default     = false
}

variable "passive_waf" {
  type        = bool
  description = "Enable passive mode for the WAF, counting all requests rather than blocking."
  default     = false
}

variable "platform" {
  type = object({
    database = object({
      host              = string
      port              = number
      admin_secret_arn  = string
      security_group_id = string
    })
    logging_bucket_domain_name = string
    logging_key_arn            = string
    name_prefix                = string
    private_subnets            = list(string)
    public_subnets             = list(string)
    vpc_id                     = string
    waf_log_group_arn          = string
  })
  description = <<-EOT
    The shared platform this deployment runs on: pass the outputs of
    sebt_platform or sebt_platform_lookup. The module names its ECR
    repositories from name_prefix, and creates its own rotating login on
    the database, using the master credentials in database.admin_secret_arn.
    EOT
}

variable "project" {
  type        = string
  description = "Base project name used for resource naming."
  default     = "sebt-portal"
}

variable "project_short" {
  type        = string
  description = "Abbreviated project name for resource naming."
  default     = "sebt"
}

variable "rate_limit_requests" {
  type        = number
  description = "Number of requests allowed in the rate limit window. Minimum of 10, or set to 0 to disable rate limiting."
  default     = 100
}

variable "rate_limit_window" {
  type        = number
  description = "Time window, in seconds, for the rate limit. Options are: 60, 120, 300, 600"
  default     = 60
}

variable "security_agent_user_agent" {
  type        = string
  description = <<-EOT
    User-Agent value used by the AWS Security Agent penetration test. When set in
    the development environment, requests with this User-Agent are allowed
    through the WAF and bypass all subsequent rules. Leave empty to disable.

    SECURITY: User-Agent is trivially spoofable and is recorded in WAF/ALB logs,
    so anyone who learns this value can bypass the WAF. The dev-only guard lives
    in locals.tf; clear this value once the engagement is complete.
    EOT
  default     = "securityagent-0609137"
}

variable "sender_email" {
  type        = string
  description = "Email address used as the sender for OTP emails."
}

variable "ses_allowed_recipients" {
  type        = list(string)
  description = "Email addresses to verify as SES recipients for sandbox testing."
  default     = []
}

variable "state" {
  type        = string
  description = "State abbreviation (e.g. DC, CO)."
}

variable "web_cpu" {
  type        = number
  description = "CPU units for the web service container."
  default     = 512
}

variable "web_memory" {
  type        = number
  description = "Memory (in MiB) for the web service container."
  default     = 1024
}

variable "seeding_enabled" {
  type        = string
  description = "Enable database seeding in non-development environments."
  default     = "false"
}

variable "seeding_email_pattern" {
  type        = string
  description = "Format string for seed user emails, where {0} is the scenario name (e.g. sebt.dc+{0}@codeforamerica.org)."
  default     = ""
}

variable "dc_source_db_name" {
  type        = string
  description = "DC-only: name of the DcSource database (alongside SebtPortal on the same RDS instance) holding the seeded mock data the DC plugin's stored procedures read from. When non-empty, the API task gets a DC_SOURCE_DB_NAME env var so Program.cs builds DCConnector:ConnectionString. Empty means the DC plugin path isn't wired (the typical case for CO and for in-memory mock mode)."
  default     = ""
}

variable "log_as_json" {
  type        = bool
  description = "Output API logs as structured JSON. Enable in deployed environments so Datadog can parse log severity."
  default     = false
}

variable "state_api_environment_variables" {
  type        = map(string)
  description = "State-specific environment variables to inject into the API container."
  default     = {}
}

variable "state_web_environment_variables" {
  type        = map(string)
  description = "State-specific environment variables to inject into the Web container."
  default     = {}
}

variable "secret_recovery_period" {
  type        = number
  description = "Number of days to retain a secret before permanent deletion."
  default     = 7
}

variable "redis_node_type" {
  type        = string
  description = "ElastiCache node instance type for the Valkey cache."
  default     = "cache.t4g.micro"
}

variable "redis_num_cache_clusters" {
  type        = number
  description = "Number of nodes in the Redis replication group (1 primary + N-1 replicas)."
  default     = 2
}

variable "redis_multi_az_enabled" {
  type        = bool
  description = "Spread the Redis primary and replica across Availability Zones."
  default     = true
}

variable "redis_automatic_failover_enabled" {
  type        = bool
  description = "Promote a Redis replica to primary automatically on primary failure."
  default     = true
}

variable "state_api_secrets" {
  type        = map(string)
  description = "State-specific secrets to inject into the API container environment, as environment variable name => key in state_secrets."
  default     = {}
}

variable "state_secrets" {
  type        = map(string)
  description = "State-specific secrets to create, as name => description. JWT_SECRET_KEY and IDENTIFIER_HASHER_SECRET_KEY always exist and are wired into the API."
  default     = {}
}

variable "state_secrets_doppler_config" {
  type        = string
  description = "Doppler config the state-specific secrets are synced from (e.g. dev_co_state_secrets)."
}

variable "state_web_secrets" {
  type        = map(string)
  description = "State-specific secrets to inject into the Web container environment, as environment variable name => key in state_secrets."
  default     = {}
}
