data "aws_vpc" "main" {
  tags = { Name = local.prefix }
}

data "aws_subnets" "private" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  tags = { use = "private" }
}

data "aws_subnets" "public" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  tags = { use = "public" }
}

data "aws_kms_alias" "logging" {
  name = "alias/${var.project}-${var.state}/${var.environment}/logs"
}

data "aws_s3_bucket" "logging" {
  bucket = "${local.prefix}-logs"
}

data "aws_cloudwatch_log_group" "waf" {
  name = "aws-waf-logs-cfa/${var.project}-${var.state}/${var.environment}"
}

data "aws_db_instance" "main" {
  db_instance_identifier = "${local.prefix}-db"
}

data "aws_security_group" "database" {
  vpc_id = data.aws_vpc.main.id
  tags   = { Name = "${local.prefix}-database" }
}
