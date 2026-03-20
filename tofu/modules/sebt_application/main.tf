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
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=1.13.0"

  project       = "${var.project}-${var.state}"
  project_short = "sebt"
  environment   = var.environment
  service       = "api"
  service_short = "api"

  domain         = var.domain
  subdomain      = "api"
  hosted_zone_id = var.hosted_zone_id

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
    STATE                                        = var.state
    DB_HOST                                      = module.database.endpoint
    DB_NAME                                      = "SebtPortal"
    DB_PORT                                      = "1433"
    "PluginAssemblyPaths__0"                     = "plugins-${lower(var.state)}"
    "SmtpClientSettings__SmtpServer"             = module.ses.smtp_server
    "SmtpClientSettings__SmtpPort"               = "587"
    "SmtpClientSettings__EnableSsl"              = "true"
    "EmailOtpSenderServiceSettings__SenderEmail" = module.ses.sender_email
    "Seeding__Enabled"                           = var.seeding_enabled
    "Seeding__EmailPattern"                      = var.seeding_email_pattern
    "UseMockHouseholdData"                       = var.use_mock_household_data
  }, var.enable_appconfig ? {
    "AppConfig__Agent__BaseUrl"          = "http://localhost:2772"
    "AppConfig__Agent__ApplicationId"    = module.appconfig[0].application_id
    "AppConfig__Agent__EnvironmentId"    = module.appconfig[0].environment_id
    "AppConfig__FeatureFlags__ProfileId" = module.appconfig[0].feature_flags_profile_id
    "AppConfig__AppSettings__ProfileId"  = module.appconfig[0].app_settings_profile_id
  } : {}, var.state_api_environment_variables)

  environment_secrets = merge({
    DB_USER                        = "${module.database.secret_arn}:username"
    DB_PASSWORD                    = "${module.database.secret_arn}:password"
    "SmtpClientSettings__UserName" = "${module.ses.secret_arn}:username"
    "SmtpClientSettings__Password" = "${module.ses.secret_arn}:password"
    "JwtSettings__SecretKey"       = "${module.secrets.secrets["app"].secret_arn}:jwt_secret_key"
    "IdentifierHasher__SecretKey"  = "${module.secrets.secrets["app"].secret_arn}:identifier_hasher_secret_key"
  }, var.state_api_environment_secrets)
}

# Create the Web service. This is a public-facing Next.js application served                                                                      
# via an internet facing Application Load Balancer. It communicates with the                                                                      
# API service internally through the VPC.                                                                                                         
module "web" {
  source  = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=1.10.0"
  project = "${var.project}-${var.state}"
  # TODO Make project_short a variable
  project_short = "sebt"
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
    STATE                    = lower(var.state)
    NEXT_PUBLIC_STATE        = lower(var.state)
    BACKEND_URL              = "https://${module.api.endpoint_url}"
  }, var.state_web_environment_variables)

  environment_secrets = var.state_web_environment_secrets
}

# Store application secrets in Secrets Manager.
module "secrets" {
  source = "github.com/codeforamerica/tofu-modules-aws-secrets?ref=2.0.0"

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "api"

  secrets = {
    "app" = {
      description     = "Application secrets for the SEBT Portal API."
      recovery_window = var.secret_recovery_period
    }
  }
}

# Create the RDS SQL Server database.
module "database" {
  source = "../sebt_database"

  project         = "${var.project}-${var.state}"
  project_short   = "sebt"
  environment     = var.environment
  vpc_id          = var.vpc_id
  subnets         = var.private_subnets
  logging_key_arn = var.logging_key_id

  ingress_security_groups = [module.api.security_group_id]

  skip_final_snapshot = var.skip_final_snapshot
  apply_immediately   = var.apply_immediately
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
  source     = "github.com/codeforamerica/tofu-modules-aws-cloudfront-waf?ref=2.1.0"
  depends_on = [module.web.load_balancer_arn]

  project        = "${var.project}-${var.state}"
  environment    = var.environment
  domain         = var.domain
  subdomain      = ""
  origin_alb_arn = module.web.load_balancer_arn
  log_bucket     = var.logging_bucket_domain_name
  log_group      = var.waf_log_group
  passive        = var.passive_waf
  hosted_zone_id = var.hosted_zone_id

  rate_limit_rules = var.rate_limit_requests > 0 ? {
    base = {
      action   = var.passive_waf ? "count" : "block"
      priority = 100
      limit    = var.rate_limit_requests
      window   = var.rate_limit_window
    }
  } : {}
}
