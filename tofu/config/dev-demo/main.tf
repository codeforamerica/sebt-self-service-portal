terraform {
  backend "s3" {
    bucket         = "sebt-portal-co-development-tfstate"
    key            = "dev-demo/backend.tfstate"
    dynamodb_table = "development.tfstate"
    region         = "us-east-1"
  }
}

# Values (doppler_token, or oidc_identity + oidc_token) are read from the
# environment (DOPPLER_TOKEN or DOPPLER_OIDC_IDENTITY/DOPPLER_OIDC_TOKEN) —
# this block just declares the provider explicitly so it's authenticated
# once and shared by every module below, rather than each module implicitly
# configuring its own instance and re-using a single-use OIDC token.
provider "doppler" {}

# The unbranded demo: Colorado's conventions (Keycloak OIDC, the CO connector on
# mock data) under its own state code. It runs in the CO development account on
# the platform and Keycloak that dev-co-shared owns.
module "platform" {
  source = "../../modules/sebt_platform_lookup"

  project     = var.project
  state       = "co"
  environment = var.environment
}

# The demo deploys the images dev-co's pipeline pushes.
data "aws_ecr_repository" "api" {
  name = "${var.project}-co-${var.environment}-api"
}

data "aws_ecr_repository" "web" {
  name = "${var.project}-co-${var.environment}-web"
}

# Created by bootstrap.
data "aws_route53_zone" "main" {
  name = var.domain
}

# Store the demo's secrets in Secrets Manager, synced from Doppler.
module "state_secrets" {
  source = "github.com/codeforamerica/tofu-modules-aws-secrets?ref=1880642d0546106d0c1f568304c0326b32b8cdbb" # 2.1.1

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  # Doppler's AWS Secrets Manager sync creates its own target secret named
  # "{path}/{DOPPLER_KEY}" (uppercase, exact match) rather than writing into
  # a pre-existing ARN — so these keys and add_suffix=false must make our
  # secret's name match exactly what Doppler will create, or Doppler ends up
  # populating an entirely separate, unreferenced secret.
  add_suffix = false

  secrets = {
    "JWT_SECRET_KEY" = {
      description     = "JWT signing secret for the SEBT Portal API."
      recovery_window = 7
    }
    "IDENTIFIER_HASHER_SECRET_KEY" = {
      description     = "Identifier hashing secret for the SEBT Portal API."
      recovery_window = 7
    }
    "OIDC_CLIENT_ID" = {
      description     = "Keycloak OIDC client ID for authentication."
      recovery_window = 7
    }
    "OIDC_CLIENT_SECRET" = {
      description     = "Keycloak OIDC client secret for authentication."
      recovery_window = 7
    }
    "OIDC_STEP_UP_CLIENT_ID" = {
      description     = "Keycloak OIDC step-up client ID for authentication."
      recovery_window = 7
    }
    "OIDC_STEP_UP_CLIENT_SECRET" = {
      description     = "Keycloak OIDC step-up client secret for authentication."
      recovery_window = 7
    }
    "OIDC_COMPLETE_LOGIN_SIGNING_KEY" = {
      description     = "Signing key for completing OIDC login."
      recovery_window = 7
    }
  }
}

module "state_secrets_doppler" {
  source     = "github.com/codeforamerica/tofu-modules-aws-doppler?ref=e8ba5edac1eaf156702c89e0c9cd84f86dcafbfc" # 1.1.0
  depends_on = [module.state_secrets]

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  kms_key_arns             = [module.state_secrets.kms_key_arn]
  doppler_project          = "safety-net-sebt-self-service-portal"
  doppler_environment_slug = "dev_demo_state_secrets"
  doppler_workspace_id     = "08430c37e2a2889dc220"
}

locals {
  keycloak_realm     = "${var.keycloak_hostname}/realms/sebt"
  keycloak_discovery = "${local.keycloak_realm}/.well-known/openid-configuration"
  keycloak_authorize = "${local.keycloak_realm}/protocol/openid-connect/auth"
  oidc_callback      = "https://${var.domain}/callback"
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source    = "../../modules/sebt_application"
  providers = { doppler = doppler }

  apply_immediately          = true
  domain                     = var.domain
  hosted_zone_id             = data.aws_route53_zone.main.zone_id
  environment                = var.environment
  image_tag                  = var.image_tag
  logging_key_id             = module.platform.logging_key_arn
  logging_bucket_domain_name = module.platform.logging_bucket_domain_name
  private_subnets            = module.platform.private_subnets
  public_subnets             = module.platform.public_subnets
  vpc_id                     = module.platform.vpc_id
  project                    = var.project
  sender_email               = var.sender_email
  state                      = var.state
  waf_log_group              = module.platform.waf_log_group_arn
  passive_waf                = true
  enable_appconfig           = false
  log_as_json                = true

  # Load balancer and target group names leave out the state, so the default
  # would collide with dev-co's in this account.
  project_short = "sebt-demo"

  database     = module.platform.database
  db_name      = "SebtPortalDemo"
  db_app_users = ["demoappuser", "demoappuser_clone"]

  api_image_url      = data.aws_ecr_repository.api.repository_url
  api_repository_arn = data.aws_ecr_repository.api.arn
  web_image_url      = data.aws_ecr_repository.web.repository_url
  web_repository_arn = data.aws_ecr_repository.web.arn

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
  desired_containers     = 1

  redis_num_cache_clusters         = 1
  redis_multi_az_enabled           = false
  redis_automatic_failover_enabled = false

  # Matches the Keycloak fixture users' emails.
  seeding_enabled       = "true"
  seeding_email_pattern = "sebt.co+{0}@codeforamerica.org"

  state_api_environment_variables = {
    # Like the CO previews: no development-only behavior on a public link.
    "ASPNETCORE_ENVIRONMENT"                     = "Staging"
    "FeatureManagement__use_mock_household_data" = "true"
    "FeatureManagement__use_mock_cbms_responses" = "true"
    "FeatureManagement__bypass_otp"              = "false"
    # The demo state has no connector of its own; it runs Colorado's on mock data.
    "PluginAssemblyPaths__0"                           = "plugins-co"
    "Socure__Enabled"                                  = "false"
    "Smarty__Enabled"                                  = "false"
    "Oidc__DiscoveryEndpoint"                          = local.keycloak_discovery
    "Oidc__AuthorizationEndpoint"                      = local.keycloak_authorize
    "Oidc__CallbackRedirectUri"                        = local.oidc_callback
    "Oidc__StepUp__DiscoveryEndpoint"                  = local.keycloak_discovery
    "Oidc__StepUp__AuthorizationEndpoint"              = local.keycloak_authorize
    "Oidc__StepUp__CallbackRedirectUri"                = local.oidc_callback
    "StateHouseholdId__PreferredHouseholdIdTypes__0"   = "Phone"
    "IdProofingRequirements__address+write"            = "IAL1plus"
    "IdProofingRequirements__email+view"               = "IAL1plus"
    "IdProofingRequirements__household+view"           = "IAL1plus"
    "IdProofingRequirements__card+write"               = "IAL1plus"
    "IdProofingValidity__ValidityDays"                 = "1826"
    "Oidc__VerificationClaims__LevelClaimName"         = "socureIdVerificationLevel"
    "Oidc__VerificationClaims__DateClaimName"          = "socureIdVerificationDate"
    "Oidc__VerificationClaims__FallbackLevelClaimName" = "myCoIdVerificationLevel"
    "Oidc__VerificationClaims__FallbackDateClaimName"  = "myCoIdVerificationDate"
  }

  state_api_environment_secrets = {
    "JwtSettings__SecretKey"        = module.state_secrets.secrets["JWT_SECRET_KEY"].secret_arn
    "IdentifierHasher__SecretKey"   = module.state_secrets.secrets["IDENTIFIER_HASHER_SECRET_KEY"].secret_arn
    "Oidc__ClientId"                = module.state_secrets.secrets["OIDC_CLIENT_ID"].secret_arn
    "Oidc__ClientSecret"            = module.state_secrets.secrets["OIDC_CLIENT_SECRET"].secret_arn
    "Oidc__StepUp__ClientId"        = module.state_secrets.secrets["OIDC_STEP_UP_CLIENT_ID"].secret_arn
    "Oidc__StepUp__ClientSecret"    = module.state_secrets.secrets["OIDC_STEP_UP_CLIENT_SECRET"].secret_arn
    "Oidc__CompleteLoginSigningKey" = module.state_secrets.secrets["OIDC_COMPLETE_LOGIN_SIGNING_KEY"].secret_arn
  }

  state_web_environment_variables = {
    OIDC_DISCOVERY_ENDPOINT = local.keycloak_discovery
    OIDC_REDIRECT_URI       = local.oidc_callback
    OIDC_LANGUAGE_PARAM     = "en"
    # Widens the CSP to the Keycloak origin (src/proxy.ts).
    OIDC_ISSUER_ORIGIN = var.keycloak_hostname
  }

  state_web_environment_secrets = {
    OIDC_CLIENT_ID                  = module.state_secrets.secrets["OIDC_CLIENT_ID"].secret_arn
    OIDC_CLIENT_SECRET              = module.state_secrets.secrets["OIDC_CLIENT_SECRET"].secret_arn
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = module.state_secrets.secrets["OIDC_COMPLETE_LOGIN_SIGNING_KEY"].secret_arn
  }
}
