locals {
  prefix    = "${var.project}-${var.state}-${var.environment}-keycloak"
  subdomain = "auth"
  hostname  = "https://${local.subdomain}.${var.domain}"
}

resource "random_password" "db" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "random_password" "admin" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "aws_secretsmanager_secret" "db" {
  name                    = "${local.prefix}-db"
  recovery_window_in_days = var.force_delete ? 0 : 7
}

resource "aws_secretsmanager_secret_version" "db" {
  secret_id = aws_secretsmanager_secret.db.id
  secret_string = jsonencode({
    username = "keycloak"
    password = random_password.db.result
    host     = aws_db_instance.keycloak.address
    port     = tostring(aws_db_instance.keycloak.port)
    dbname   = aws_db_instance.keycloak.db_name
  })
}

resource "aws_secretsmanager_secret" "admin" {
  name                    = "${local.prefix}-admin"
  recovery_window_in_days = var.force_delete ? 0 : 7
}

resource "aws_secretsmanager_secret_version" "admin" {
  secret_id = aws_secretsmanager_secret.admin.id
  secret_string = jsonencode({
    username = "admin"
    password = random_password.admin.result
  })
}

resource "aws_db_subnet_group" "keycloak" {
  name       = "${local.prefix}-db"
  subnet_ids = var.private_subnets

  tags = {
    Name = "${local.prefix}-db"
  }
}

resource "aws_security_group" "database" {
  name_prefix = "${local.prefix}-db-"
  description = "Postgres for shared Keycloak"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${local.prefix}-db"
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group_rule" "database_ingress" {
  type              = "ingress"
  from_port         = 5432
  to_port           = 5432
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.database.id
  description       = "Postgres from VPC (Keycloak tasks)"
}

resource "aws_security_group_rule" "database_egress" {
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.database.id
}

resource "aws_db_instance" "keycloak" {
  identifier = "${local.prefix}-db"

  engine                = "postgres"
  engine_version        = "16"
  instance_class        = "db.t4g.micro"
  allocated_storage     = 20
  max_allocated_storage = 50
  storage_type          = "gp3"
  storage_encrypted     = true

  db_name  = "keycloak"
  username = "keycloak"
  password = random_password.db.result

  db_subnet_group_name   = aws_db_subnet_group.keycloak.name
  vpc_security_group_ids = [aws_security_group.database.id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period = 1
  skip_final_snapshot     = var.skip_final_snapshot
  deletion_protection     = false
  apply_immediately       = true

  tags = {
    Name = "${local.prefix}-db"
  }
}

# Public Fargate service: browsers redirect here for OIDC.
module "service" {
  source = "github.com/codeforamerica/tofu-modules-aws-fargate-service?ref=10d4c56eb6c156c7a670ee19e40caad476c81a1b" # 1.14.0

  project       = "${var.project}-${var.state}"
  project_short = "sebt"
  environment   = var.environment
  service       = "keycloak"
  service_short = "kc"

  domain         = var.domain
  subdomain      = local.subdomain
  hosted_zone_id = var.hosted_zone_id

  # Public for OIDC (/realms/*). /admin* is denied at the ALB unless the
  # bypass header or an allowlisted CIDR matches — see admin_access.tf.
  public          = true
  create_endpoint = true

  vpc_id          = var.vpc_id
  private_subnets = var.private_subnets
  public_subnets  = var.public_subnets

  logging_key_id = var.logging_key_id

  container_port    = 8080
  health_check_path = "/realms/sebt"
  # Realm import + first Postgres migration can be slow.
  health_check_grace_period = 300

  create_repository = false
  image_url         = var.image_url
  repository_arn    = var.repository_arn
  image_tag         = var.image_tag

  cpu    = 512
  memory = 1024

  desired_containers     = var.desired_containers
  enable_execute_command = true
  image_tags_mutable     = true
  force_delete           = var.force_delete

  container_command = [
    "start",
    "--optimized",
    "--import-realm",
    "--http-enabled=true",
    "--proxy-headers=xforwarded",
    "--hostname-strict=false",
  ]

  environment_variables = {
    KC_HOSTNAME                     = local.hostname
    KC_HOSTNAME_BACKCHANNEL_DYNAMIC = "true"
    KC_HTTP_ENABLED                 = "true"
    KC_PROXY_HEADERS                = "xforwarded"
    KC_HEALTH_ENABLED               = "true"
    KC_DB                           = "postgres"
    KC_DB_URL                       = "jdbc:postgresql://${aws_db_instance.keycloak.address}:${aws_db_instance.keycloak.port}/${aws_db_instance.keycloak.db_name}"
    KC_DB_USERNAME                  = "keycloak"
    KC_BOOTSTRAP_ADMIN_USERNAME     = "admin"
  }

  environment_secrets = {
    KC_DB_PASSWORD              = "${aws_secretsmanager_secret.db.arn}:password"
    KC_BOOTSTRAP_ADMIN_PASSWORD = "${aws_secretsmanager_secret.admin.arn}:password"
  }

  depends_on = [
    aws_db_instance.keycloak,
    aws_secretsmanager_secret_version.db,
    aws_secretsmanager_secret_version.admin,
  ]
}
