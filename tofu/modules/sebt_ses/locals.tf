locals {
  prefix      = "${var.project}-${var.environment}"
  smtp_server = "email-smtp.${data.aws_region.current.name}.${data.aws_partition.current.dns_suffix}"
}
