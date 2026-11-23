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

# Look up ECR repositories created by bootstrap.
data "aws_ecr_repository" "api" {
  name = "${var.project}-${var.state}-${var.environment}-api"
}

data "aws_ecr_repository" "web" {
  name = "${var.project}-${var.state}-${var.environment}-web"
}

# Look up the hosted zone for DNS records.
data "aws_route53_zone" "main" {
  name = "dc.sebt-portal.codeforamerica.app"
}

# Look up the enrollment checker hosted zone (created by bootstrap).
data "aws_route53_zone" "enrollment_checker" {
  name = "dc.sebt-enrollment.codeforamerica.app"
}

# Store DC-specific secrets in Secrets Manager. Each key represents a
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
    "SOCURE_API_KEY" = {
      description     = "Socure API key for identity verification."
      recovery_window = 7
    }
    "SOCURE_WEBHOOK_SECRET" = {
      description     = "Socure webhook signing secret for identity verification."
      recovery_window = 7
    }
    "PII_ENCRYPTION_KEY_ID" = {
      description     = "Active PII encryption key ID for the SEBT Portal API (AES-256-GCM)."
      recovery_window = 7
    }
    "PII_ENCRYPTION_KEY_MATERIAL_BASE64" = {
      description     = "Active PII encryption key material for the SEBT Portal API (AES-256-GCM)."
      recovery_window = 7
    }
  }
}

# Sync DC's state-specific secrets to Doppler.
module "state_secrets_doppler" {
  source     = "github.com/codeforamerica/tofu-modules-aws-doppler?ref=e8ba5edac1eaf156702c89e0c9cd84f86dcafbfc" # 1.1.0
  depends_on = [module.state_secrets]

  project     = "${var.project}-${var.state}"
  environment = var.environment
  service     = "state-secrets"

  kms_key_arns             = [module.state_secrets.kms_key_arn]
  doppler_project          = "safety-net-sebt-self-service-portal"
  doppler_environment_slug = "dev_dc_state_secrets"
  doppler_workspace_id     = "08430c37e2a2889dc220"
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
  enable_appconfig           = true
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
  desired_containers     = 1

  seeding_enabled       = "true"
  seeding_email_pattern = "sebt.dc+{0}@codeforamerica.org"
  dc_source_db_name     = "DcSource"

  state_api_environment_variables = {
    "IdProofingRequirements__household+view__application"        = "IAL1"
    "IdProofingRequirements__household+view__coloadedStreamline" = "IAL1"
    "IdProofingRequirements__household+view__streamline"         = "IAL1plus"
  }

  state_api_environment_secrets = {
    "JwtSettings__SecretKey"                    = module.state_secrets.secrets["JWT_SECRET_KEY"].secret_arn
    "IdentifierHasher__SecretKey"               = module.state_secrets.secrets["IDENTIFIER_HASHER_SECRET_KEY"].secret_arn
    "Socure__ApiKey"                            = module.state_secrets.secrets["SOCURE_API_KEY"].secret_arn
    "Socure__WebhookSecret"                     = module.state_secrets.secrets["SOCURE_WEBHOOK_SECRET"].secret_arn
    "PiiEncryption__Keys__0__KeyId"             = module.state_secrets.secrets["PII_ENCRYPTION_KEY_ID"].secret_arn
    "PiiEncryption__Keys__0__KeyMaterialBase64" = module.state_secrets.secrets["PII_ENCRYPTION_KEY_MATERIAL_BASE64"].secret_arn
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
  state_web_environment_secrets = {}
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
