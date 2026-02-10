# Create the API service. This is an internal service that is only accessible
# within the VPC. It runs the .NET backend API on Fargate behind an internal
# Application Load Balancer.
module "api" {
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=1.10.0"

  project       = "sebt-portal"
  project_short = "sebt"
  environment   = var.environment
  service       = "api"
  service_short = "api"

  domain    = var.domain
  subdomain = "api"

  public          = false
  create_endpoint = true

  vpc_id          = var.vpc_id
  private_subnets = var.private_subnets

  logging_key_id = var.logging_key_id

  container_port    = 8080
  health_check_path = "/health"

  image_tag = var.image_tag

  cpu    = 512
  memory = 1024

  desired_containers     = var.desired_containers
  enable_execute_command = var.enable_execute_command
  image_tags_mutable     = var.image_tags_mutable
  force_delete           = var.force_delete

  environment_variables = {
    ASPNETCORE_ENVIRONMENT = var.environment
  }
}

