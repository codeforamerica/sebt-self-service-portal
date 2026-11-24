terraform {
  backend "s3" {
    bucket         = "sebt-portal-co-development-tfstate"
    key            = "dev-co/backend.tfstate"
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

# The network, logging, bastion and RDS instance are owned by dev-co-shared.
module "platform" {
  source = "../../modules/sebt_platform_lookup"

  project     = var.project
  state       = var.state
  environment = var.environment
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source    = "../../modules/sebt_application"
  providers = { doppler = doppler }

  apply_immediately = true
  domain            = var.domain
  # Cover pr-N / api-pr-N preview hosts on the shared ALBs (ACM one-level wildcard).
  certificate_sans = ["*.${var.domain}"]
  # Serve pr-N preview hosts through CloudFront (same wildcard as certificate_sans).
  cloudfront_extra_aliases = ["*.${var.domain}"]
  hosted_zone_name         = "co.sebt-portal.codeforamerica.app"
  environment              = var.environment
  image_tag                = var.image_tag
  platform                 = module.platform
  project                  = var.project
  sender_email             = var.sender_email
  state                    = var.state
  passive_waf              = true
  log_as_json              = true

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
  enable_appconfig       = true
  desired_containers     = 2

  state_secrets_doppler_config = "dev_co_state_secrets"

  state_secrets = {
    "CBMS_CLIENT_ID"                  = "OAuth 2.0 client ID for the Colorado CBMS SEBT API."
    "CBMS_CLIENT_SECRET"              = "OAuth 2.0 client secret for the Colorado CBMS SEBT API."
    "OIDC_CLIENT_ID"                  = "MyColorado OIDC client ID for authentication."
    "OIDC_CLIENT_SECRET"              = "MyColorado OIDC client secret for authentication."
    "OIDC_STEP_UP_CLIENT_ID"          = "MyColorado OIDC step-up client ID for authentication."
    "OIDC_STEP_UP_CLIENT_SECRET"      = "MyColorado OIDC step-up client secret for authentication."
    "OIDC_COMPLETE_LOGIN_SIGNING_KEY" = "Signing key for completing MyColorado OIDC login."
  }

  state_api_environment_variables = {
    "Oidc__DiscoveryEndpoint"                          = var.oidc_discovery_endpoint
    "Oidc__AuthorizationEndpoint"                      = var.oidc_authorization_endpoint
    "Oidc__CallbackRedirectUri"                        = "https://${var.domain}/callback"
    "Oidc__StepUp__DiscoveryEndpoint"                  = var.oidc_discovery_endpoint
    "Oidc__StepUp__AuthorizationEndpoint"              = var.oidc_authorization_endpoint
    "Oidc__StepUp__CallbackRedirectUri"                = "https://${var.domain}/callback"
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
    "Cbms__ClientId"                = "CBMS_CLIENT_ID"
    "Cbms__ClientSecret"            = "CBMS_CLIENT_SECRET"
    "Oidc__ClientId"                = "OIDC_CLIENT_ID"
    "Oidc__ClientSecret"            = "OIDC_CLIENT_SECRET"
    "Oidc__StepUp__ClientId"        = "OIDC_STEP_UP_CLIENT_ID"
    "Oidc__StepUp__ClientSecret"    = "OIDC_STEP_UP_CLIENT_SECRET"
    "Oidc__CompleteLoginSigningKey" = "OIDC_COMPLETE_LOGIN_SIGNING_KEY"
  }

  state_web_environment_variables = {
    ENROLLMENT_CHECKER_ORIGIN = "https://dev.co.sebt-enrollment.codeforamerica.app"
    OIDC_DISCOVERY_ENDPOINT   = var.oidc_discovery_endpoint
    OIDC_REDIRECT_URI         = "https://${var.domain}/callback"
    OIDC_LANGUAGE_PARAM       = "en"

    # Browser-facing config: the Next server reads these per request and serves
    # them to the client, so they are set on the container rather than baked into
    # the image (docs/adr/0023-runtime-client-config.md). An empty value leaves
    # that integration off. Preview stacks clone this task definition, so they
    # inherit these too.
    AMPLITUDE_API_KEY           = var.amplitude_api_key
    APPLICATION_URL             = var.application_url
    APPLICATION_URL_PEAK_PARAMS = var.application_url_peak_params
    MIXPANEL_TOKEN              = var.mixpanel_token
    SITEIMPROVE_ID              = var.siteimprove_id
    SMARTY_EMBEDDED_KEY         = var.smarty_embedded_key
  }

  state_web_secrets = {
    OIDC_CLIENT_ID                  = "OIDC_CLIENT_ID"
    OIDC_CLIENT_SECRET              = "OIDC_CLIENT_SECRET"
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = "OIDC_COMPLETE_LOGIN_SIGNING_KEY"
  }
}

# Deploy the enrollment checker as a static site behind CloudFront.
module "enrollment_checker" {
  source = "../../modules/sebt_enrollment_checker"

  project                    = var.project
  state                      = var.state
  environment                = var.environment
  domain                     = "dev.co.sebt-enrollment.codeforamerica.app"
  hosted_zone_id             = data.aws_route53_zone.enrollment_checker.zone_id
  logging_bucket_domain_name = module.platform.logging_bucket_domain_name
  logging_bucket_name        = module.platform.logging_bucket
  force_delete               = true
}
