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

  project = var.project
  # The demo runs in the CO account, so its platform is named for CO.
  state       = "co"
  environment = var.environment
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source    = "../../modules/sebt_application"
  providers = { doppler = doppler }

  apply_immediately = true
  domain            = var.domain
  hosted_zone_name  = var.domain
  environment       = var.environment
  image_tag         = var.image_tag
  platform          = module.platform
  project           = var.project
  sender_email      = var.sender_email
  state             = var.state
  passive_waf       = true
  enable_appconfig  = false
  log_as_json       = true

  # Load balancer and target group names leave out the state, so the default
  # would collide with dev-co's in this account.
  project_short = "sebt-demo"

  db_name      = "SebtPortalDemo"
  db_app_users = ["demoappuser", "demoappuser_clone"]

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

  state_secrets_doppler_config = "dev_demo_state_secrets"

  state_secrets = {
    "OIDC_CLIENT_ID"                  = "Keycloak OIDC client ID for authentication."
    "OIDC_CLIENT_SECRET"              = "Keycloak OIDC client secret for authentication."
    "OIDC_STEP_UP_CLIENT_ID"          = "Keycloak OIDC step-up client ID for authentication."
    "OIDC_STEP_UP_CLIENT_SECRET"      = "Keycloak OIDC step-up client secret for authentication."
    "OIDC_COMPLETE_LOGIN_SIGNING_KEY" = "Signing key for completing OIDC login."
  }

  state_api_environment_variables = {
    # Like the CO previews: no development-only behavior on a public link.
    "ASPNETCORE_ENVIRONMENT"                     = "Staging"
    "FeatureManagement__use_mock_household_data" = "true"
    "FeatureManagement__use_mock_cbms_responses" = "true"
    "FeatureManagement__bypass_otp"              = "false"
    # Every start (a deploy, scaling up, or power.sh reset) is a fresh demo.
    "Seeding__ClearDataOnStartup" = "true"
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

  state_api_secrets = {
    "Oidc__ClientId"                = "OIDC_CLIENT_ID"
    "Oidc__ClientSecret"            = "OIDC_CLIENT_SECRET"
    "Oidc__StepUp__ClientId"        = "OIDC_STEP_UP_CLIENT_ID"
    "Oidc__StepUp__ClientSecret"    = "OIDC_STEP_UP_CLIENT_SECRET"
    "Oidc__CompleteLoginSigningKey" = "OIDC_COMPLETE_LOGIN_SIGNING_KEY"
  }

  state_web_environment_variables = {
    OIDC_DISCOVERY_ENDPOINT = local.keycloak_discovery
    OIDC_REDIRECT_URI       = local.oidc_callback
    OIDC_LANGUAGE_PARAM     = "en"
    # Widens the CSP to the Keycloak origin (src/proxy.ts).
    OIDC_ISSUER_ORIGIN = var.keycloak_hostname
  }

  state_web_secrets = {
    OIDC_CLIENT_ID                  = "OIDC_CLIENT_ID"
    OIDC_CLIENT_SECRET              = "OIDC_CLIENT_SECRET"
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = "OIDC_COMPLETE_LOGIN_SIGNING_KEY"
  }
}
