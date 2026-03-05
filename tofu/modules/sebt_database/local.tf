locals {
  prefix        = "${var.project}-${var.environment}"
  project_short = var.project_short != "" ? var.project_short : var.project
  short_prefix  = "${local.project_short}-${var.environment}"
  port          = 1433

  security_group_rules = merge(
    length(var.ingress_cidrs) == 0 ? {} : {
      ingress_cidrs = {
        type        = "ingress"
        protocol    = "tcp"
        from_port   = local.port
        to_port     = local.port
        cidr_blocks = var.ingress_cidrs
      }
    },
    {
      for idx, sg in var.ingress_security_groups : "ingress_sg_${idx}" => {
        type                     = "ingress"
        protocol                 = "tcp"
        from_port                = local.port
        to_port                  = local.port
        source_security_group_id = sg
      }
    }
  )
}

