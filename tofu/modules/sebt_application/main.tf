# Manage feature flags and application settings via AWS AppConfig.
module "appconfig" {
  source = "../sebt_appconfig"
  count  = var.enable_appconfig ? 1 : 0

  project     = "${var.project}-${var.state}"
  environment = var.environment
}

# Create the API service. This is an internal service that is only accessible
# within the VPC. It runs the .NET backend API on Fargate behind an internal
# Application Load Balancer.
module "api" {
  # feat/certificate-sans adds optional ACM SANs for preview hostnames.
  # Bump to a released tag once that lands (expected >= 1.15.0).
  # Keep this branch pin until certificate_sans ships in a release (> 1.14.0).
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=feat/certificate-sans"

  project       = "${var.project}-${var.state}"
  project_short = var.project_short
  environment   = var.environment
  service       = "api"
  service_short = "api"

  domain           = var.domain
  subdomain        = "api"
  hosted_zone_id   = var.hosted_zone_id
  certificate_sans = var.certificate_sans

  public          = false
  create_endpoint = true

  vpc_id          = var.vpc_id
  private_subnets = var.private_subnets

  logging_key_id = var.logging_key_id

  container_port    = 8080
  health_check_path = "/health"

  create_repository = false
  image_url         = var.api_image_url
  repository_arn    = var.api_repository_arn
  image_tag         = var.image_tag

  cpu    = var.api_cpu
  memory = var.api_memory

  desired_containers     = var.desired_containers
  enable_execute_command = var.enable_execute_command
  image_tags_mutable     = var.image_tags_mutable
  force_delete           = var.force_delete

  enable_appconfig_agent         = var.enable_appconfig
  appconfig_agent_application_id = var.enable_appconfig ? module.appconfig[0].application_id : ""
  appconfig_agent_environment_variables = var.enable_appconfig ? {
    PREFETCH_LIST = join(",", [
      "/applications/${module.appconfig[0].application_id}/environments/${module.appconfig[0].environment_id}/configurations/${module.appconfig[0].feature_flags_profile_id}",
      "/applications/${module.appconfig[0].application_id}/environments/${module.appconfig[0].environment_id}/configurations/${module.appconfig[0].app_settings_profile_id}",
    ])
  } : {}

  environment_variables = merge({
    ASPNETCORE_ENVIRONMENT                       = var.environment
    LOG_FORMAT                                   = var.log_as_json ? "json" : "text"
    STATE                                        = var.state
    DB_HOST                                      = module.database.endpoint
    DB_NAME                                      = "SebtPortal"
    DB_PORT                                      = "1433"
    "Redis__Host"                                = module.redis.primary_endpoint
    "Redis__Port"                                = tostring(module.redis.port)
    "Redis__Ssl"                                 = "true"
    "Redis__SslHost"                             = module.redis.primary_endpoint
    "PluginAssemblyPaths__0"                     = "plugins-${lower(var.state)}"
    "SmtpClientSettings__SmtpServer"             = module.ses.smtp_server
    "SmtpClientSettings__SmtpPort"               = "587"
    "SmtpClientSettings__EnableSsl"              = "true"
    "EmailOtpSenderServiceSettings__SenderEmail" = module.ses.sender_email
    "Seeding__Enabled"                           = var.seeding_enabled
    "Seeding__EmailPattern"                      = var.seeding_email_pattern
    "Seeding__State"                             = lower(var.state)
    }, var.enable_appconfig ? {
    "AppConfig__Agent__BaseUrl"          = "http://localhost:2772"
    "AppConfig__Agent__ApplicationId"    = module.appconfig[0].application_id
    "AppConfig__Agent__EnvironmentId"    = module.appconfig[0].environment_id
    "AppConfig__FeatureFlags__ProfileId" = module.appconfig[0].feature_flags_profile_id
    "AppConfig__AppSettings__ProfileId"  = module.appconfig[0].app_settings_profile_id
    } : {}, var.dc_source_db_name != "" ? {
    "DC_SOURCE_DB_NAME" = var.dc_source_db_name
  } : {}, var.state_api_environment_variables)

  environment_secrets = merge({
    DB_USER                        = "${module.database.app_user_secret_arn}:username"
    DB_PASSWORD                    = "${module.database.app_user_secret_arn}:password"
    "Redis__Password"              = "${module.redis.auth_token_secret_arn}:auth_token"
    "SmtpClientSettings__UserName" = "${module.ses.secret_arn}:username"
    "SmtpClientSettings__Password" = "${module.ses.secret_arn}:password"
    "Smarty__AuthId"               = module.secrets.secrets["SMARTY_AUTH_ID"].secret_arn
    "Smarty__AuthToken"            = module.secrets.secrets["SMARTY_AUTH_TOKEN"].secret_arn
  }, var.state_api_environment_secrets)

  # Forward application traces to Datadog APM when the integration API key is
  # available. Metrics pipelines are unchanged from the default ADOT config.
  otel_collector_version = "v0.47.0"
  otel_config = local.otel_override_config ? templatefile("${path.module}/templates/otel-config.yaml.tftpl", {
    app_namespace = "${var.project}-${var.state}/api"
    environment   = var.environment
  }) : null
  otel_secrets = local.otel_secrets
}

# Create the Web service. This is a public-facing Next.js application served                                                                      
# via an internet facing Application Load Balancer. It communicates with the                                                                      
# API service internally through the VPC.                                                                                                         
module "web" {
  source        = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=10d4c56eb6c156c7a670ee19e40caad476c81a1b" # 1.14.0
  project       = "${var.project}-${var.state}"
  project_short = var.project_short
  environment   = var.environment
  service       = "web"
  service_short = "web"

  domain                  = var.domain
  subdomain               = "origin"
  hosted_zone_id          = var.hosted_zone_id
  ingress_prefix_list_ids = [data.aws_ec2_managed_prefix_list.cloudfront.id]

  public          = false
  create_endpoint = true

  vpc_id          = var.vpc_id
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets

  logging_key_id = var.logging_key_id

  container_port    = 3000
  health_check_path = "/"

  create_repository = false
  image_url         = var.web_image_url
  repository_arn    = var.web_repository_arn
  image_tag         = var.image_tag

  cpu    = var.web_cpu
  memory = var.web_memory

  desired_containers     = var.desired_containers
  enable_execute_command = var.enable_execute_command
  image_tags_mutable     = var.image_tags_mutable
  force_delete           = var.force_delete

  environment_variables = merge({
    STATE             = lower(var.state)
    NEXT_PUBLIC_STATE = lower(var.state)
    BACKEND_URL       = "https://${module.api.endpoint_url}"
    # Emit OTLP to the collector sidecar (declared below); mirrors the API.
    OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
    OTEL_SERVICE_NAME           = "sebt-portal-web"
    OTEL_DEPLOYMENT_ENVIRONMENT = var.environment
  }, var.state_web_environment_variables)

  environment_secrets = var.state_web_environment_secrets

  # ADOT collector sidecar, mirroring module.api (forwards traces to Datadog APM
  # when the integration key is present).
  otel_collector_version = "v0.47.0"
  otel_config = local.otel_override_config ? templatefile("${path.module}/templates/otel-config.yaml.tftpl", {
    app_namespace = "${var.project}-${var.state}/web"
    environment   = var.environment
  }) : null
  otel_secrets = local.otel_secrets
}

# Store application secrets in Secrets Manager.
module "secrets" {
  source = "github.com/codeforamerica/tofu-modules-aws-secrets?ref=1880642d0546106d0c1f568304c0326b32b8cdbb" # 2.1.1

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "api"

  # Doppler's AWS Secrets Manager sync creates its own target secret named
  # "{path}/{DOPPLER_KEY}" (uppercase, exact match) rather than writing into
  # a pre-existing ARN — so these keys and add_suffix=false must make our
  # secret's name match exactly what Doppler will create, or Doppler ends up
  # populating an entirely separate, unreferenced secret.
  add_suffix = false

  secrets = {
    "SMARTY_AUTH_ID" = {
      description     = "Smarty address validation API auth ID."
      recovery_window = var.secret_recovery_period
    }
    "SMARTY_AUTH_TOKEN" = {
      description     = "Smarty address validation API auth token."
      recovery_window = var.secret_recovery_period
    }
  }
}

# Sync the API's secrets to Doppler so they can be managed from a single
# place instead of the AWS console. Smarty's credentials are a single shared
# vendor account used by every state, so both DC and CO read from the same
# root "dev" config instead of a per-state branch.
module "doppler" {
  source     = "github.com/codeforamerica/tofu-modules-aws-doppler?ref=e8ba5edac1eaf156702c89e0c9cd84f86dcafbfc" # 1.1.0
  depends_on = [module.secrets]

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "api"

  kms_key_arns             = [module.secrets.kms_key_arn]
  doppler_project          = "safety-net-sebt-self-service-portal"
  doppler_environment_slug = "dev"
  doppler_workspace_id     = "08430c37e2a2889dc220"
}

# Create the RDS SQL Server database.
module "database" {
  source = "../sebt_database"

  project         = "${var.project}-${var.state}"
  project_short   = var.project_short
  environment     = var.environment
  vpc_id          = var.vpc_id
  subnets         = var.private_subnets
  logging_key_arn = var.logging_key_id

  ingress_security_groups = [module.api.security_group_id]
  ingress_cidrs           = var.db_ingress_cidrs

  db_name             = "SebtPortal"
  additional_db_names = var.dc_source_db_name != "" ? [var.dc_source_db_name] : []
  ecs_cluster_name    = module.api.cluster_name
  ecs_service_name    = local.api_ecs_service_name

  skip_final_snapshot = var.skip_final_snapshot
  apply_immediately   = var.apply_immediately
}

# Create the ElastiCache (Valkey) replication group. Provides the shared
# distributed cache that backs HybridCache (L2) and distributed locking, and —
# critically — the cross-container OIDC pre-auth session store.
module "redis" {
  source = "../sebt_redis"

  project       = "${var.project}-${var.state}"
  project_short = var.project_short
  environment   = var.environment
  vpc_id        = var.vpc_id
  subnets       = var.private_subnets

  ingress_security_groups = [module.api.security_group_id]

  node_type                  = var.redis_node_type
  num_cache_clusters         = var.redis_num_cache_clusters
  multi_az_enabled           = var.redis_multi_az_enabled
  automatic_failover_enabled = var.redis_automatic_failover_enabled

  secret_recovery_period = var.secret_recovery_period
  apply_immediately      = var.apply_immediately
}

# Create the SES domain identity, DNS records, and SMTP credentials.
module "ses" {
  source = "../sebt_ses"

  project        = "${var.project}-${var.state}"
  environment    = var.environment
  domain         = var.domain
  hosted_zone_id = var.hosted_zone_id

  sender_email       = var.sender_email
  allowed_recipients = var.ses_allowed_recipients

  ecs_cluster_name = module.api.cluster_name
  ecs_service_name = module.api.cluster_name
}

module "cloudfront_waf" {
  source     = "github.com/codeforamerica/tofu-modules-aws-cloudfront-waf?ref=2.8.0"
  depends_on = [module.web.load_balancer_arn]

  project          = "${var.project}-${var.state}"
  environment      = var.environment
  domain           = var.domain
  subdomain        = ""
  certificate_sans = var.certificate_sans
  extra_aliases    = var.cloudfront_extra_aliases
  origin_alb_arn   = module.web.load_balancer_arn
  log_bucket       = var.logging_bucket_domain_name
  log_group        = var.waf_log_group
  passive          = var.passive_waf
  hosted_zone_id   = var.hosted_zone_id

  # AWS Security Agent penetration test bypass (development only). Evaluated
  # before the rate-limit rule so the test's traffic is allowed and terminating.
  webhooks          = local.security_agent_waf_rules
  webhooks_priority = 1

  rate_limit_rules = var.rate_limit_requests > 0 ? {
    base = {
      action   = var.passive_waf ? "count" : "block"
      priority = 100
      limit    = var.rate_limit_requests
      window   = var.rate_limit_window
    }
  } : {}
}
