# The RDS instance moved out of module.app, which now only manages the API's
# login on it.

moved {
  from = module.app.module.database.aws_kms_key.database
  to   = module.database.aws_kms_key.database
}

moved {
  from = module.app.module.database.aws_kms_alias.database
  to   = module.database.aws_kms_alias.database
}

moved {
  from = module.app.module.database.aws_db_subnet_group.main
  to   = module.database.aws_db_subnet_group.main
}

moved {
  from = module.app.module.database.aws_security_group.database
  to   = module.database.aws_security_group.database
}

moved {
  from = module.app.module.database.aws_security_group_rule.this
  to   = module.database.aws_security_group_rule.this
}

moved {
  from = module.app.module.database.aws_cloudwatch_log_group.error
  to   = module.database.aws_cloudwatch_log_group.error
}

moved {
  from = module.app.module.database.aws_cloudwatch_log_group.agent
  to   = module.database.aws_cloudwatch_log_group.agent
}

moved {
  from = module.app.module.database.aws_db_instance.main
  to   = module.database.aws_db_instance.main
}

moved {
  from = module.app.aws_cloudwatch_log_subscription_filter.datadog
  to   = module.database.aws_cloudwatch_log_subscription_filter.datadog
}
