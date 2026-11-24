# sebt_platform_lookup finds this module's instance and security group by name.
# Its tests/naming.tftest.hcl asserts the same names for the same inputs, with
# the platform's project of "<project>-<state>".

mock_provider "aws" {
  mock_resource "aws_kms_key" {
    defaults = { arn = "arn:aws:kms:us-east-1:123456789012:key/mock-key" }
  }
  mock_resource "aws_db_instance" {
    defaults = {
      master_user_secret = [{
        kms_key_id    = "mock"
        secret_arn    = "arn:aws:secretsmanager:us-east-1:123456789012:secret:mock"
        secret_status = "active"
      }]
    }
  }
  mock_data "aws_caller_identity" {
    defaults = { account_id = "123456789012" }
  }
}

variables {
  project                 = "sebt-portal-co"
  project_short           = "sebt"
  environment             = "development"
  vpc_id                  = "vpc-mock"
  subnets                 = ["subnet-mock1", "subnet-mock2"]
  logging_key_arn         = "arn:aws:kms:us-east-1:123456789012:key/mock-logging-key"
  ingress_security_groups = []
}

run "names_resources_from_project_and_environment" {
  command = plan

  assert {
    condition     = aws_db_instance.main.identifier == "sebt-portal-co-development-db"
    error_message = "The database identifier must be <project>-<environment>-db."
  }

  assert {
    condition     = aws_security_group.database.tags["Name"] == "sebt-portal-co-development-database"
    error_message = "The security group's Name tag must be <project>-<environment>-database."
  }
}
