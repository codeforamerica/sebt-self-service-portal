locals {
  prefix        = "${var.project}-${var.environment}"
  project_short = var.project_short != "" ? var.project_short : var.project
  short_prefix  = "${local.project_short}-${var.environment}"
  smtp_server   = "email-smtp.${data.aws_region.current.name}.${data.aws_partition.current.dns_suffix}"
}
