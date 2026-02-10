module "backend" {
  source = "github.com/codeforamerica/tofu-modules-aws-backend?ref=1.1.2"

  project     = var.project
  environment = var.environment
}
