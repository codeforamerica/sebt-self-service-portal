output "application_id" {
  description = "ID of the AppConfig application."
  value       = aws_appconfig_application.this.id
}

output "environment_id" {
  description = "ID of the AppConfig environment."
  value       = aws_appconfig_environment.this.environment_id
}

output "feature_flags_profile_id" {
  description = "ID of the feature flags configuration profile."
  value       = aws_appconfig_configuration_profile.feature_flags.configuration_profile_id
}

output "app_settings_profile_id" {
  description = "ID of the app settings configuration profile."
  value       = aws_appconfig_configuration_profile.app_settings.configuration_profile_id
}
