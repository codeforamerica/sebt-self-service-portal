# The network, logging, bastion and RDS instance moved to dev-dc-shared, whose
# state was seeded with these entries. Forget them here without destroying
# anything.

removed {
  from = module.logging
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.vpc
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.bastion
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_kms_key.database
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_kms_alias.database
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_db_subnet_group.main
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_security_group.database
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_security_group_rule.this
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_cloudwatch_log_group.error
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_cloudwatch_log_group.agent
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.module.database.aws_db_instance.main
  lifecycle {
    destroy = false
  }
}

removed {
  from = module.app.aws_cloudwatch_log_subscription_filter.datadog
  lifecycle {
    destroy = false
  }
}
