# The app-user login and its rotation moved out of the database module, which
# now only owns the RDS instance.

moved {
  from = module.database.aws_secretsmanager_secret.app_user
  to   = module.app_user.aws_secretsmanager_secret.app_user
}

moved {
  from = module.database.random_password.app_user_initial
  to   = module.app_user.random_password.app_user_initial
}

moved {
  from = module.database.aws_secretsmanager_secret_version.app_user_initial
  to   = module.app_user.aws_secretsmanager_secret_version.app_user_initial
}

moved {
  from = module.database.aws_security_group.rotation_lambda
  to   = module.app_user.aws_security_group.rotation_lambda
}

moved {
  from = module.database.aws_security_group_rule.rotation_lambda_egress_db
  to   = module.app_user.aws_security_group_rule.rotation_lambda_egress_db
}

moved {
  from = module.database.aws_security_group_rule.rotation_lambda_egress_https
  to   = module.app_user.aws_security_group_rule.rotation_lambda_egress_https
}

moved {
  from = module.database.aws_security_group_rule.db_ingress_rotation_lambda
  to   = module.app_user.aws_security_group_rule.db_ingress_rotation_lambda
}

moved {
  from = module.database.aws_iam_role.rotation_lambda
  to   = module.app_user.aws_iam_role.rotation_lambda
}

moved {
  from = module.database.aws_iam_role_policy_attachment.rotation_lambda_vpc_execution
  to   = module.app_user.aws_iam_role_policy_attachment.rotation_lambda_vpc_execution
}

moved {
  from = module.database.aws_iam_role_policy.rotation_lambda
  to   = module.app_user.aws_iam_role_policy.rotation_lambda
}

moved {
  from = module.database.aws_cloudwatch_log_group.rotation_lambda
  to   = module.app_user.aws_cloudwatch_log_group.rotation_lambda
}

moved {
  from = module.database.aws_lambda_function.rotation
  to   = module.app_user.aws_lambda_function.rotation
}

moved {
  from = module.database.aws_lambda_permission.secrets_manager
  to   = module.app_user.aws_lambda_permission.secrets_manager
}

moved {
  from = module.database.aws_secretsmanager_secret_rotation.app_user
  to   = module.app_user.aws_secretsmanager_secret_rotation.app_user
}
