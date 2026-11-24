terraform {
  backend "s3" {
    bucket         = "sebt-portal-dc-development-tfstate"
    key            = "dev-dc/backend.tfstate"
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

# The network, logging, bastion and RDS instance are owned by dev-dc-shared.
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
  hosted_zone_name  = "dc.sebt-portal.codeforamerica.app"
  environment       = var.environment
  image_tag         = var.image_tag
  platform          = module.platform
  project           = var.project
  sender_email      = var.sender_email
  state             = var.state
  passive_waf       = true
  enable_appconfig  = true
  log_as_json       = true

  force_delete           = true
  image_tags_mutable     = true
  enable_execute_command = true
  desired_containers     = 1

  seeding_enabled       = "true"
  seeding_email_pattern = "sebt.dc+{0}@codeforamerica.org"
  dc_source_db_name     = "DcSource"

  state_secrets_doppler_config = "dev_dc_state_secrets"

  state_secrets = {
    "SOCURE_API_KEY"                     = "Socure API key for identity verification."
    "SOCURE_WEBHOOK_SECRET"              = "Socure webhook signing secret for identity verification."
    "PII_ENCRYPTION_KEY_ID"              = "Active PII encryption key ID for the SEBT Portal API (AES-256-GCM)."
    "PII_ENCRYPTION_KEY_MATERIAL_BASE64" = "Active PII encryption key material for the SEBT Portal API (AES-256-GCM)."
  }

  state_api_environment_variables = {
    "IdProofingRequirements__household+view__application"        = "IAL1"
    "IdProofingRequirements__household+view__coloadedStreamline" = "IAL1"
    "IdProofingRequirements__household+view__streamline"         = "IAL1plus"
  }

  state_api_secrets = {
    "Socure__ApiKey"                            = "SOCURE_API_KEY"
    "Socure__WebhookSecret"                     = "SOCURE_WEBHOOK_SECRET"
    "PiiEncryption__Keys__0__KeyId"             = "PII_ENCRYPTION_KEY_ID"
    "PiiEncryption__Keys__0__KeyMaterialBase64" = "PII_ENCRYPTION_KEY_MATERIAL_BASE64"
  }

  state_web_environment_variables = {
    ENROLLMENT_CHECKER_ORIGIN = "https://dev.dc.sebt-enrollment.codeforamerica.app"

    # Browser-facing config: the Next server reads these per request and serves
    # them to the client, so they are set on the container rather than baked into
    # the image (docs/adr/0023-runtime-client-config.md). An empty value leaves
    # that integration off.
    AMPLITUDE_API_KEY           = var.amplitude_api_key
    APPLICATION_URL             = var.application_url
    APPLICATION_URL_PEAK_PARAMS = var.application_url_peak_params
    MIXPANEL_TOKEN              = var.mixpanel_token
    SITEIMPROVE_ID              = var.siteimprove_id
    SMARTY_EMBEDDED_KEY         = var.smarty_embedded_key
    SOCURE_DI_SDK_KEY           = var.socure_di_sdk_key
  }
}

# DC-only: ECS task definition + IAM role for one-off DcSource seed runs.
# Runs on the existing API cluster, reuses the API security group and RDS
# master credentials. ECR repo is provisioned in bootstrap.
module "dc_source_seed" {
  source = "../../modules/sebt_dc_source_seed"

  project           = "${var.project}-${var.state}"
  environment       = var.environment
  cluster_name      = module.app.api_cluster_name
  subnet_ids        = module.platform.private_subnets
  security_group_id = module.app.api_security_group_id
  db_endpoint       = module.platform.database.host
  db_secret_arn     = module.platform.database.admin_secret_arn
  logging_key_arn   = module.platform.logging_key_arn
}

# Deploy the enrollment checker as a static site behind CloudFront.
module "enrollment_checker" {
  source = "../../modules/sebt_enrollment_checker"

  project                    = var.project
  state                      = var.state
  environment                = var.environment
  domain                     = "dev.dc.sebt-enrollment.codeforamerica.app"
  hosted_zone_id             = data.aws_route53_zone.enrollment_checker.zone_id
  logging_bucket_domain_name = module.platform.logging_bucket_domain_name
  logging_bucket_name        = module.platform.logging_bucket
  force_delete               = true
}
