# Create the API service. This is an internal service that is only accessible
# within the VPC. It runs the .NET backend API on Fargate behind an internal
# Application Load Balancer.
module "api" {
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=1.10.0"

  project       = "sebt-portal"
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

  cpu    = 512
  memory = 1024

  desired_containers     = var.desired_containers
  enable_execute_command = var.enable_execute_command
  image_tags_mutable     = var.image_tags_mutable
  force_delete           = var.force_delete

  environment_variables = {
    ASPNETCORE_ENVIRONMENT                       = var.environment
    STATE                                        = var.state
    DB_HOST                                      = module.database.endpoint
    DB_NAME                                      = "SebtPortal"
    DB_PORT                                      = "1433"
    "PluginAssemblyPaths__0"                     = "plugins-${lower(var.state)}"
    "JwtSettings__SecretKey"                     = var.jwt_secret_key
    "IdentifierHasher__SecretKey"                = var.identifier_hasher_secret_key
    "SmtpClientSettings__SmtpServer"             = module.ses.smtp_server
    "SmtpClientSettings__SmtpPort"               = "587"
    "SmtpClientSettings__EnableSsl"              = "true"
    "EmailOtpSenderServiceSettings__SenderEmail" = module.ses.sender_email
  }

  environment_secrets = {
    DB_USER                        = "${module.database.secret_arn}:username"
    DB_PASSWORD                    = "${module.database.secret_arn}:password"
    "SmtpClientSettings__UserName" = "${module.ses.secret_arn}:username"
    "SmtpClientSettings__Password" = "${module.ses.secret_arn}:password"
  }
}

# Create the Web service. This is a public-facing Next.js application served                                                                      
# via an internet facing Application Load Balancer. It communicates with the                                                                      
# API service internally through the VPC.                                                                                                         
module "web" {
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=1.10.0"

  project       = "sebt-portal"
  project_short = "sebt"
  environment   = var.environment
  service       = "web"
  service_short = "web"

  domain         = var.domain
  subdomain      = ""
  hosted_zone_id = var.hosted_zone_id

  public          = true
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

  cpu    = 512
  memory = 1024

  desired_containers     = var.desired_containers
  enable_execute_command = var.enable_execute_command
  image_tags_mutable     = var.image_tags_mutable
  force_delete           = var.force_delete

  environment_variables = {
    STATE                    = lower(var.state)
    NEXT_PUBLIC_STATE        = lower(var.state)
    NEXT_PUBLIC_API_BASE_URL = "https://${module.api.endpoint_url}"
    BACKEND_URL              = "https://${module.api.endpoint_url}"
  }
}

# Create the RDS SQL Server database.
module "database" {
  source = "../sebt_database"

  project         = "sebt-portal"
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

  project        = "sebt-portal"
  environment    = var.environment
  domain         = var.domain
  hosted_zone_id = var.hosted_zone_id

  sender_email       = var.sender_email
  allowed_recipients = var.ses_allowed_recipients
}

