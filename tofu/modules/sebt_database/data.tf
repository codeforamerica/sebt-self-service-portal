data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}
data "aws_region" "current" {}

data "aws_rds_engine_version" "this" {
  engine  = var.engine
  version = var.engine_version
  latest  = true
}
