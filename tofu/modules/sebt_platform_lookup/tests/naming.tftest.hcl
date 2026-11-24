# The lookup finds what sebt_platform creates, by name. Its tests/naming.tftest.hcl
# asserts the same names for the same inputs; change one side and both must
# change. Each assertion reads the argument a data source is queried with.

mock_provider "aws" {
  mock_data "aws_db_instance" {
    defaults = {
      address = "mock.example.test"
      port    = 1433
      master_user_secret = [{
        kms_key_id    = "mock"
        secret_arn    = "arn:aws:secretsmanager:us-east-1:123456789012:secret:mock"
        secret_status = "active"
      }]
    }
  }
  mock_data "aws_kms_alias" {
    defaults = { target_key_arn = "arn:aws:kms:us-east-1:123456789012:key/mock-key" }
  }
}

variables {
  project     = "sebt-portal"
  state       = "co"
  environment = "development"
}

run "finds_resources_by_project_state_and_environment" {
  command = plan

  assert {
    condition     = output.name_prefix == "sebt-portal-co-development"
    error_message = "name_prefix must be project-state-environment."
  }

  assert {
    condition     = data.aws_db_instance.main.db_instance_identifier == "sebt-portal-co-development-db"
    error_message = "The database identifier must be <prefix>-db."
  }

  assert {
    condition     = data.aws_security_group.database.tags["Name"] == "sebt-portal-co-development-database"
    error_message = "The database security group's Name tag must be <prefix>-database."
  }

  assert {
    condition     = data.aws_cloudwatch_log_group.waf.name == "aws-waf-logs-cfa/sebt-portal-co/development"
    error_message = "The WAF log group must be aws-waf-logs-cfa/<project>-<state>/<environment>."
  }

  assert {
    condition     = data.aws_s3_bucket.logging.bucket == "sebt-portal-co-development-logs"
    error_message = "The logging bucket must be <prefix>-logs."
  }

  assert {
    condition     = data.aws_kms_alias.logging.name == "alias/sebt-portal-co/development/logs"
    error_message = "The logging KMS alias must be alias/<project>-<state>/<environment>/logs."
  }

  assert {
    condition     = data.aws_vpc.main.tags["Name"] == "sebt-portal-co-development"
    error_message = "The VPC's Name tag must be the prefix."
  }

  assert {
    condition     = data.aws_subnets.private.tags["use"] == "private" && data.aws_subnets.public.tags["use"] == "public"
    error_message = "Subnets must be found by their use tag."
  }
}
