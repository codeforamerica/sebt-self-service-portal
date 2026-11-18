locals {
  prefix        = "${var.project}-${var.environment}"
  project_short = var.project_short != "" ? var.project_short : var.project
  short_prefix  = "${local.project_short}-${var.environment}"
}
