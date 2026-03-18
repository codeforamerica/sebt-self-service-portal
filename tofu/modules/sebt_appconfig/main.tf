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
