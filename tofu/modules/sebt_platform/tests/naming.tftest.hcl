# sebt_platform_lookup finds the platform's resources by name, so these names
# are a contract. Its tests/naming.tftest.hcl asserts the same names for the
# same inputs.
#
# Only names reachable from the root module can be asserted: outputs of the
# upstream modules. The VPC "Name" tag, the subnet "use" tags and the WAF log
# group name are set inside modules whose resources a test cannot read, so the
# lookup's tests are the only check on them. The database names are asserted in
# sebt_database's tests.

mock_provider "aws" {
  mock_data "aws_availability_zones" {
    defaults = { names = ["us-east-1a", "us-east-1b"] }
  }
  mock_data "aws_iam_policy_document" {
    defaults = { json = "{\"Version\":\"2012-10-17\",\"Statement\":[]}" }
  }
  mock_data "aws_caller_identity" {
    defaults = { account_id = "123456789012" }
  }
  mock_data "aws_region" {
    defaults = { name = "us-east-1", region = "us-east-1" }
  }
  mock_resource "aws_kms_key" {
    defaults = { arn = "arn:aws:kms:us-east-1:123456789012:key/mock-key" }
  }
  mock_resource "aws_cloudwatch_log_group" {
    defaults = { arn = "arn:aws:logs:us-east-1:123456789012:log-group:mock" }
  }
  mock_resource "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::123456789012:role/mock" }
  }
  mock_resource "aws_iam_policy" {
    defaults = { arn = "arn:aws:iam::123456789012:policy/mock" }
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
}

variables {
  project         = "sebt-portal"
  state           = "co"
  environment     = "development"
  domain          = "co.example.test"
  vpc_cidr        = "10.0.0.0/16"
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]
}

run "names_resources_from_project_state_and_environment" {
  command = plan

  assert {
    condition     = output.name_prefix == "sebt-portal-co-development"
    error_message = "name_prefix must be project-state-environment."
  }

  assert {
    condition     = output.logging_bucket == "sebt-portal-co-development-logs"
    error_message = "The logging bucket must be named <prefix>-logs."
  }

  assert {
    condition     = module.logging.kms_key_alias == "alias/sebt-portal-co/development/logs"
    error_message = "The logging KMS alias must be alias/<project>-<state>/<environment>/logs."
  }
}
