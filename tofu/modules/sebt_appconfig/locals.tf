locals {
  prefix = "${var.project}-${var.environment}"

  # Convert the feature_flags map into the AWS AppConfig feature flag JSON
  # format: { "version": "1", "flags": {...}, "values": {...} }
  feature_flags_content = jsonencode({
    version = "1"
    flags = {
      for name, config in var.feature_flags : name => {}
    }
    values = {
      for name, config in var.feature_flags : name => {
        enabled = config.enabled
      }
    }
  })

  # Encode app settings as plain JSON.
  app_settings_content = jsonencode(var.app_settings)
}
