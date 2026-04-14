terraform {
  backend "s3" {
    bucket         = "sebt-portal-co-development-tfstate"
    key            = "dev-co/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Create an S3 bucket and KMS key for logging.                                                                                                    
module "logging" {
  source = "github.com/codeforamerica/tofu-modules-aws-logging?ref=2.1.0"

  project     = "${var.project}-${var.state}"
  environment = var.environment
  log_groups = {
    "waf" = {
      name = "aws-waf-logs-cfa/${var.project}-${var.state}/${var.environment}"
      tags = {
        source = "waf"
        webacl = "${var.project}-${var.state}-${var.environment}"
        domain = var.domain
      }
    }
  }

  log_groups_to_datadog = true
}

# Create a VPC with public and private subnets. Since this is a dev
# environment, we'll use a single NAT gateway to reduce costs.
module "vpc" {
  source = "github.com/codeforamerica/tofu-modules-aws-vpc?ref=1.1.2"

  project            = "${var.project}-${var.state}"
  environment        = var.environment
  single_nat_gateway = true
  logging_key_id     = module.logging.kms_key_arn

  cidr            = var.vpc_cidr
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets
}

# Look up ECR repositories created by bootstrap.
data "aws_ecr_repository" "api" {
  name = "${var.project}-${var.state}-${var.environment}-api"
}

data "aws_ecr_repository" "web" {
  name = "${var.project}-${var.state}-${var.environment}-web"
}

# Look up the hosted zone for DNS records.
data "aws_route53_zone" "main" {
  name = "co.sebt-portal.codeforamerica.app"
}

# Store Colorado-specific secrets in Secrets Manager. Each block represents a
# separate secret for a specific service or integration.
module "state_secrets" {
  source = "github.com/codeforamerica/tofu-modules-aws-secrets?ref=2.0.0"

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  secrets = {
    "cbms" = {
      description     = "OAuth 2.0 client credentials for the Colorado CBMS SEBT API."
      recovery_window = 7
    }
    "oidc" = {
      description     = "MyColorado OIDC credentials for authentication."
      recovery_window = 7
    }
  }
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source = "../../modules/sebt_application"

  apply_immediately          = true
  domain                     = var.domain
  hosted_zone_id             = data.aws_route53_zone.main.zone_id
  environment                = var.environment
  image_tag                  = var.image_tag
  logging_key_id             = module.logging.kms_key_arn
  logging_bucket_domain_name = module.logging.bucket_domain_name
  private_subnets            = module.vpc.private_subnets
  project                    = var.project
  public_subnets             = module.vpc.public_subnets
  sender_email               = var.sender_email
  skip_final_snapshot        = true
  state                      = var.state
  vpc_id                     = module.vpc.vpc_id
  waf_log_group              = module.logging.log_groups["waf"]
  passive_waf                = true
  log_as_json                = true

  api_image_url      = data.aws_ecr_repository.api.repository_url
  api_repository_arn = data.aws_ecr_repository.api.arn
  web_image_url      = data.aws_ecr_repository.web.repository_url
  web_repository_arn = data.aws_ecr_repository.web.arn

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
  enable_appconfig       = true

  state_api_environment_variables = {
    "Oidc__DiscoveryEndpoint"                          = var.oidc_discovery_endpoint
    "Oidc__AuthorizationEndpoint"                      = var.oidc_authorization_endpoint
    "Oidc__CallbackRedirectUri"                        = "https://${var.domain}/callback"
    "Oidc__LanguageParam"                              = "en"
    "Oidc__StepUp__DiscoveryEndpoint"                  = var.oidc_discovery_endpoint
    "Oidc__StepUp__AuthorizationEndpoint"              = var.oidc_authorization_endpoint
    "Oidc__StepUp__CallbackRedirectUri"                = "https://${var.domain}/callback"
    "StateHouseholdId__PreferredHouseholdIdTypes__0"   = "Phone"
    "MinimumIal__ApplicationCases"                     = "IAL1"
    "MinimumIal__CoLoadedStreamlineCases"               = "IAL1"
    "MinimumIal__NonCoLoadedStreamlineCases"             = "IAL1"
  }

  state_api_environment_secrets = {
    "Cbms__ClientId"                = "${module.state_secrets.secrets["cbms"].secret_arn}:client_id"
    "Cbms__ClientSecret"            = "${module.state_secrets.secrets["cbms"].secret_arn}:client_secret"
    "Oidc__ClientId"                = "${module.state_secrets.secrets["oidc"].secret_arn}:client_id"
    "Oidc__ClientSecret"            = "${module.state_secrets.secrets["oidc"].secret_arn}:client_secret"
    "Oidc__StepUp__ClientId"        = "${module.state_secrets.secrets["oidc"].secret_arn}:step_up_client_id"
    "Oidc__StepUp__ClientSecret"    = "${module.state_secrets.secrets["oidc"].secret_arn}:step_up_client_secret"
    "Oidc__CompleteLoginSigningKey" = "${module.state_secrets.secrets["oidc"].secret_arn}:complete_login_signing_key"
  }

  state_web_environment_variables = {
    OIDC_DISCOVERY_ENDPOINT = var.oidc_discovery_endpoint
    OIDC_REDIRECT_URI       = "https://${var.domain}/callback"
    OIDC_LANGUAGE_PARAM     = "en"
  }

  state_web_environment_secrets = {
    OIDC_CLIENT_ID                  = "${module.state_secrets.secrets["oidc"].secret_arn}:client_id"
    OIDC_CLIENT_SECRET              = "${module.state_secrets.secrets["oidc"].secret_arn}:client_secret"
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = "${module.state_secrets.secrets["oidc"].secret_arn}:complete_login_signing_key"
  }
}
