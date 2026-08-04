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

data "aws_ecr_repository" "keycloak" {
  name = "${var.project}-${var.state}-${var.environment}-keycloak"
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

  secrets = {
    "jwt_secret_key" = {
      description     = "JWT signing secret for the SEBT Portal API."
      recovery_window = 7
    }
    "identifier_hasher_secret_key" = {
      description     = "Identifier hashing secret for the SEBT Portal API."
      recovery_window = 7
    }
    "cbms_client_id" = {
      description     = "OAuth 2.0 client ID for the Colorado CBMS SEBT API."
      recovery_window = 7
    }
    "cbms_client_secret" = {
      description     = "OAuth 2.0 client secret for the Colorado CBMS SEBT API."
      recovery_window = 7
    }
    "oidc_client_id" = {
      description     = "MyColorado OIDC client ID for authentication."
      recovery_window = 7
    }
    "oidc_client_secret" = {
      description     = "MyColorado OIDC client secret for authentication."
      recovery_window = 7
    }
    "oidc_step_up_client_id" = {
      description     = "MyColorado OIDC step-up client ID for authentication."
      recovery_window = 7
    }
    "oidc_step_up_client_secret" = {
      description     = "MyColorado OIDC step-up client secret for authentication."
      recovery_window = 7
    }
    "oidc_complete_login_signing_key" = {
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
  source = "../../modules/sebt_application"

  apply_immediately          = true
  domain                     = var.domain
  # Cover pr-N / api-pr-N preview hosts on the shared ALBs (ACM one-level wildcard).
  certificate_sans           = ["*.${var.domain}"]
  # Serve pr-N preview hosts through CloudFront (same wildcard as certificate_sans).
  cloudfront_extra_aliases   = ["*.${var.domain}"]
  hosted_zone_id             = data.aws_route53_zone.main.zone_id
  environment                = var.environment
  image_tag                  = var.image_tag
  logging_key_id             = module.logging.kms_key_arn
  logging_bucket_domain_name = module.logging.bucket_domain_name
  private_subnets            = module.vpc.private_subnets
  public_subnets             = module.vpc.public_subnets
  vpc_id                     = module.vpc.vpc_id
  db_ingress_cidrs           = [var.vpc_cidr]
  project                    = var.project
  sender_email               = var.sender_email
  skip_final_snapshot        = true
  state                      = var.state
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
    "JwtSettings__SecretKey"        = module.state_secrets.secrets["jwt_secret_key"].secret_arn
    "IdentifierHasher__SecretKey"   = module.state_secrets.secrets["identifier_hasher_secret_key"].secret_arn
    "Cbms__ClientId"                = module.state_secrets.secrets["cbms_client_id"].secret_arn
    "Cbms__ClientSecret"            = module.state_secrets.secrets["cbms_client_secret"].secret_arn
    "Oidc__ClientId"                = module.state_secrets.secrets["oidc_client_id"].secret_arn
    "Oidc__ClientSecret"            = module.state_secrets.secrets["oidc_client_secret"].secret_arn
    "Oidc__StepUp__ClientId"        = module.state_secrets.secrets["oidc_step_up_client_id"].secret_arn
    "Oidc__StepUp__ClientSecret"    = module.state_secrets.secrets["oidc_step_up_client_secret"].secret_arn
    "Oidc__CompleteLoginSigningKey" = module.state_secrets.secrets["oidc_complete_login_signing_key"].secret_arn
  }

  state_web_environment_variables = {
    ENROLLMENT_CHECKER_ORIGIN = "https://dev.co.sebt-enrollment.codeforamerica.app"
    OIDC_DISCOVERY_ENDPOINT   = var.oidc_discovery_endpoint
    OIDC_REDIRECT_URI         = "https://${var.domain}/callback"
    OIDC_LANGUAGE_PARAM       = "en"
  }

  state_web_environment_secrets = {
    OIDC_CLIENT_ID                  = module.state_secrets.secrets["oidc_client_id"].secret_arn
    OIDC_CLIENT_SECRET              = module.state_secrets.secrets["oidc_client_secret"].secret_arn
    OIDC_COMPLETE_LOGIN_SIGNING_KEY = module.state_secrets.secrets["oidc_complete_login_signing_key"].secret_arn
  }
}

# SSM bastion for developer DB access. Uses pure-SSM port forwarding;
# no PEM distribution, no SSH. Access is IAM-gated via SSO.
module "bastion" {
  source = "github.com/codeforamerica/tofu-modules-aws-ssm-bastion?ref=1.1.0"

  project                 = "${var.project}-${var.state}"
  environment             = var.environment
  private_subnet_ids      = module.vpc.private_subnets
  vpc_id                  = module.vpc.vpc_id
  kms_key_recovery_period = 7
  instance_profile        = null
}

# Deploy the enrollment checker as a static site behind CloudFront.
module "enrollment_checker" {
  source = "../../modules/sebt_enrollment_checker"

  project                    = var.project
  state                      = var.state
  environment                = var.environment
  domain                     = "dev.co.sebt-enrollment.codeforamerica.app"
  hosted_zone_id             = data.aws_route53_zone.enrollment_checker.zone_id
  logging_bucket_domain_name = module.logging.bucket_domain_name
  logging_bucket_name        = module.logging.bucket
  force_delete               = true
}

# Shared Keycloak IdP.
# Push an image before the first apply: ./scripts/preview/build-keycloak.sh
module "preview_keycloak" {
  count  = var.enable_preview_keycloak ? 1 : 0
  source = "../../modules/sebt_keycloak"

  project         = var.project
  state           = var.state
  environment     = var.environment
  domain          = var.domain
  hosted_zone_id  = data.aws_route53_zone.main.zone_id
  vpc_id          = module.vpc.vpc_id
  private_subnets = module.vpc.private_subnets
  public_subnets  = module.vpc.public_subnets
  vpc_cidr        = var.vpc_cidr
  logging_key_id  = module.logging.kms_key_arn
  image_url       = data.aws_ecr_repository.keycloak.repository_url
  repository_arn  = data.aws_ecr_repository.keycloak.arn
  image_tag       = var.keycloak_image_tag
  force_delete    = true
  skip_final_snapshot = true
}
