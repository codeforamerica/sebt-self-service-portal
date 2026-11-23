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

# Store Colorado-specific secrets in Secrets Manager. Each key represents a
# separate secret for a specific service or integration.
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
    "CBMS_CLIENT_ID" = {
      description     = "OAuth 2.0 client ID for the Colorado CBMS SEBT API."
      recovery_window = 7
    }
    "CBMS_CLIENT_SECRET" = {
      description     = "OAuth 2.0 client secret for the Colorado CBMS SEBT API."
      recovery_window = 7
    }
    "OIDC_CLIENT_ID" = {
      description     = "MyColorado OIDC client ID for authentication."
      recovery_window = 7
    }
    "OIDC_CLIENT_SECRET" = {
      description     = "MyColorado OIDC client secret for authentication."
      recovery_window = 7
    }
    "OIDC_STEP_UP_CLIENT_ID" = {
      description     = "MyColorado OIDC step-up client ID for authentication."
      recovery_window = 7
    }
    "OIDC_STEP_UP_CLIENT_SECRET" = {
      description     = "MyColorado OIDC step-up client secret for authentication."
      recovery_window = 7
    }
    "OIDC_COMPLETE_LOGIN_SIGNING_KEY" = {
      description     = "Signing key for completing MyColorado OIDC login."
      recovery_window = 7
    }
  }
}

# Sync Colorado's state-specific secrets to Doppler.
module "state_secrets_doppler" {
  source     = "github.com/codeforamerica/tofu-modules-aws-doppler?ref=e8ba5edac1eaf156702c89e0c9cd84f86dcafbfc" # 1.1.0
  depends_on = [module.state_secrets]

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  kms_key_arns             = [module.state_secrets.kms_key_arn]
  doppler_project          = "safety-net-sebt-self-service-portal"
  doppler_environment_slug = "dev_co_state_secrets"
  doppler_workspace_id     = "08430c37e2a2889dc220"
}

# Look up the enrollment checker hosted zone (created by bootstrap).
data "aws_route53_zone" "enrollment_checker" {
  name = "co.sebt-enrollment.codeforamerica.app"
}

# Deploy the application services (API + Web) using the shared wrapper module.
module "app" {
  source    = "../../modules/sebt_application"
  providers = { doppler = doppler }

  apply_immediately          = true
  domain                     = var.domain
  # Cover pr-N / api-pr-N preview hosts on the shared ALBs (ACM one-level wildcard).
  certificate_sans           = ["*.${var.domain}"]
  # Serve pr-N preview hosts through CloudFront (same wildcard as certificate_sans).
  cloudfront_extra_aliases   = ["*.${var.domain}"]
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
  log_as_json                = true

  # module.app creates the API's login on the shared instance.
  database = module.platform.database

  api_image_url      = data.aws_ecr_repository.api.repository_url
  api_repository_arn = data.aws_ecr_repository.api.arn
  web_image_url      = data.aws_ecr_repository.web.repository_url
  web_repository_arn = data.aws_ecr_repository.web.arn

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
  enable_appconfig       = true
  desired_containers     = 2

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

  state_api_environment_secrets = {
    "JwtSettings__SecretKey"        = module.state_secrets.secrets["JWT_SECRET_KEY"].secret_arn
    "IdentifierHasher__SecretKey"   = module.state_secrets.secrets["IDENTIFIER_HASHER_SECRET_KEY"].secret_arn
    "Cbms__ClientId"                = module.state_secrets.secrets["CBMS_CLIENT_ID"].secret_arn
    "Cbms__ClientSecret"            = module.state_secrets.secrets["CBMS_CLIENT_SECRET"].secret_arn
    "Oidc__ClientId"                = module.state_secrets.secrets["OIDC_CLIENT_ID"].secret_arn
    "Oidc__ClientSecret"            = module.state_secrets.secrets["OIDC_CLIENT_SECRET"].secret_arn
    "Oidc__StepUp__ClientId"        = module.state_secrets.secrets["OIDC_STEP_UP_CLIENT_ID"].secret_arn
    "Oidc__StepUp__ClientSecret"    = module.state_secrets.secrets["OIDC_STEP_UP_CLIENT_SECRET"].secret_arn
    "Oidc__CompleteLoginSigningKey" = module.state_secrets.secrets["OIDC_COMPLETE_LOGIN_SIGNING_KEY"].secret_arn
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

  state_web_environment_secrets = {
    OIDC_CLIENT_ID                  = module.state_secrets.secrets["OIDC_CLIENT_ID"].secret_arn
    OIDC_CLIENT_SECRET              = module.state_secrets.secrets["OIDC_CLIENT_SECRET"].secret_arn
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = module.state_secrets.secrets["OIDC_COMPLETE_LOGIN_SIGNING_KEY"].secret_arn
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
