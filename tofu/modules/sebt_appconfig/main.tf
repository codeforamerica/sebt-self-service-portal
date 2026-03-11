# AWS AppConfig application — one per state/environment combination.
resource "aws_appconfig_application" "this" {
  name        = local.prefix
  description = "Configuration for ${var.project} ${var.environment}."
}

# AppConfig environment — maps to the deployment target.
resource "aws_appconfig_environment" "this" {
  name           = var.environment
  description    = "${var.environment} environment for ${var.project}."
  application_id = aws_appconfig_application.this.id
}

# Configuration profile for feature flags. Uses the AWS AppConfig feature flag content type.
resource "aws_appconfig_configuration_profile" "feature_flags" {
  application_id = aws_appconfig_application.this.id
  name           = "${local.prefix}-feature-flags"
  description    = "Feature flags for ${var.project} ${var.environment}."
  location_uri   = "hosted"
  type           = "AWS.AppConfig.FeatureFlags"
}

# Configuration profile for non-flag application settings. Uses freeform JSON
# for arbitrary key-value configuration.
resource "aws_appconfig_configuration_profile" "app_settings" {
  application_id = aws_appconfig_application.this.id
  name           = "${local.prefix}-app-settings"
  description    = "Application settings for ${var.project} ${var.environment}."
  location_uri   = "hosted"
  type           = "AWS.Freeform"
}

# Hosted configuration version for feature flags. A new version is created
# whenever the flag values change.
resource "aws_appconfig_hosted_configuration_version" "feature_flags" {
  application_id           = aws_appconfig_application.this.id
  configuration_profile_id = aws_appconfig_configuration_profile.feature_flags.configuration_profile_id
  content_type             = "application/json"
  content                  = local.feature_flags_content

  description = "Feature flags managed by OpenTofu."

  lifecycle {
    create_before_destroy = true
  }
}

# Hosted configuration version for app settings. A new version is created
# whenever the settings change.
resource "aws_appconfig_hosted_configuration_version" "app_settings" {
  application_id           = aws_appconfig_application.this.id
  configuration_profile_id = aws_appconfig_configuration_profile.app_settings.configuration_profile_id
  content_type             = "application/json"
  content                  = local.app_settings_content

  description = "Application settings managed by OpenTofu."

  lifecycle {
    create_before_destroy = true
  }
}

# Deploy the feature flags configuration to the environment.
resource "aws_appconfig_deployment" "feature_flags" {
  application_id           = aws_appconfig_application.this.id
  environment_id           = aws_appconfig_environment.this.environment_id
  configuration_profile_id = aws_appconfig_configuration_profile.feature_flags.configuration_profile_id
  configuration_version    = aws_appconfig_hosted_configuration_version.feature_flags.version_number
  deployment_strategy_id   = var.deployment_strategy

  description = "Feature flags deployment managed by OpenTofu."
}

# Deploy the app settings configuration to the environment.
resource "aws_appconfig_deployment" "app_settings" {
  application_id           = aws_appconfig_application.this.id
  environment_id           = aws_appconfig_environment.this.environment_id
  configuration_profile_id = aws_appconfig_configuration_profile.app_settings.configuration_profile_id
  configuration_version    = aws_appconfig_hosted_configuration_version.app_settings.version_number
  deployment_strategy_id   = var.deployment_strategy

  description = "Application settings deployment managed by OpenTofu."
}
