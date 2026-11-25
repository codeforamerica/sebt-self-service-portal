locals {
  keycloak_realm     = "${var.keycloak_hostname}/realms/sebt"
  keycloak_discovery = "${local.keycloak_realm}/.well-known/openid-configuration"
  keycloak_authorize = "${local.keycloak_realm}/protocol/openid-connect/auth"
  oidc_callback      = "https://${var.domain}/callback"
}
