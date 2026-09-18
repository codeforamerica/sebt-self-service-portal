// Resources specific to CO. Redis lives here rather than in shared.mts because only
// CO's appsettings declares a Redis section — it backs the CBMS household cache, while
// DC runs HybridCache L1-only.

import { resolve } from "node:path";

import { EndpointProperty, refExpr } from "../.aspire/modules/aspire.mjs";
import type {
  DistributedApplicationBuilder,
  KeycloakResource,
  ProjectResource,
  RedisResource,
} from "../.aspire/modules/aspire.mjs";
import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";

/**
 * Keycloak's host port, pinned rather than allocated.
 *
 * Keycloak mints the issuer it stamps into tokens from the address it is reached on, so
 * browser and API have to agree on one URL. 8180 is what compose,
 * appsettings.keycloak.example.json, and docs/development/keycloak-oidc.md already use,
 * which keeps the admin console at the address the guide tells people to open.
 */
const keycloakPort = 8180;

/** Realm imported from docker/keycloak/sebt-realm.json. */
const realm = "sebt";

/**
 * The only redirect URI the realm's two clients register, so the portal has to be
 * reachable here for a login to complete. Kept as a literal to stay in step with
 * sebt-realm.json rather than following the portal's allocated port.
 */
const portalCallbackUrl = "http://localhost:3000/callback";

export interface CoResources {
  /** Distributed cache backing the CBMS household cache. */
  redis: RedisResource;
  /** Local OIDC provider standing in for MyColorado. */
  keycloak: KeycloakResource;
}

export async function addCoResources(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  api: ProjectResource,
): Promise<CoResources> {
  // Supplied explicitly rather than letting Aspire generate it, so the value can be
  // handed to the API and used with redis-cli.
  const redisPassword = await builder.addParameter("redis-password", {
    value: config.redisPassword,
    secret: true,
  });

  // TLS mirrors compose, which serves Redis over TLS to match Elasticache in-transit
  // encryption. Aspire terminates TLS with the developer certificate and exposes both a
  // plain and a `rediss://` endpoint. Requires `aspire certs trust` once per machine —
  // without a trusted certificate Aspire silently serves the plain endpoint only.
  //
  // Because that certificate is trusted, the API validates it instead of bypassing CA
  // trust, so AcceptSelfSignedCertificates stays false below.
  const redis = await builder
    .addRedis("redis", { password: redisPassword })
    // Explicit, though Aspire applies this to Redis containers by default.
    .withHttpsDeveloperCertificate()
    // Both UIs run as child resources, replacing compose's redis-commander service.
    .withRedisCommander()
    .withRedisInsight();

  // Counterintuitively, the endpoint named `tcp` is the TLS one; the plain port is named
  // `secondary`. Aspire promotes TLS to the primary endpoint when a certificate exists.
  const endpoint = await redis.getEndpoint("tcp");

  await api
    .withEnvironment("Redis__Host", await endpoint.property(EndpointProperty.Host))
    .withEnvironment("Redis__Port", await endpoint.property(EndpointProperty.Port))
    .withEnvironment("Redis__Ssl", "true")
    // The developer certificate is issued for localhost, so that is the name to validate.
    .withEnvironment("Redis__SslHost", "localhost")
    .withEnvironment("Redis__AcceptSelfSignedCertificates", "false")
    .withEnvironment("Redis__Password", redisPassword)
    .waitFor(redis);

  // Stands in for MyColorado so OIDC login, logout, and step-up can be exercised without a
  // third-party IdP, replacing the compose `keycloak` profile. See
  // docs/adr/0019-keycloak-local-oidc-stand-in.md and docs/development/keycloak-oidc.md.
  //
  // Local only, and deliberately so: the realm ships fixture users sharing one password,
  // and these are the admin credentials the guide documents. Supplied rather than letting
  // Aspire generate a password, so the admin console stays reachable with what the guide
  // says to type.
  const keycloakAdmin = await builder.addParameter("keycloak-admin", {
    value: "admin",
  });
  const keycloakAdminPassword = await builder.addParameter(
    "keycloak-admin-password",
    { value: "admin", secret: true },
  );

  // No data volume, which keeps the realm in the container's ephemeral store exactly as
  // compose had it. That preserves the reset loop the guide documents: edit
  // sebt-realm.json, recreate the container, and the import runs again.
  const keycloak = await builder
    .addKeycloak("keycloak", {
      port: keycloakPort,
      adminUsername: keycloakAdmin,
      adminPassword: keycloakAdminPassword,
    })
    .withRealmImport(resolve(repoRoot, "docker/keycloak/sebt-realm.json"))
    // The custom `sebt` login theme the realm selects. Realm import covers the import
    // directory only, so the theme needs its own mount.
    .withBindMount(
      resolve(repoRoot, "docker/keycloak/themes"),
      "/opt/keycloak/themes",
      { isReadOnly: true },
    );

  const discoveryEndpoint = refExpr`${await keycloak.getEndpoint(
    "http",
  )}/realms/${realm}/.well-known/openid-configuration`;

  // Supplies what docs/development/keycloak-oidc.md otherwise asks a developer to
  // hand-merge from appsettings.keycloak.example.json. Only the example is tracked, with
  // no appsettings.co.json, so without this the CO graph boots with no OIDC client at all.
  //
  // The client ids and secrets are the realm's own fixtures, already published in that
  // example file and meaningless outside this container. Oidc:CompleteLoginSigningKey is
  // left alone: the placeholder in appsettings.json already clears its 32-character
  // minimum.
  await api
    .withEnvironment("Oidc__DiscoveryEndpoint", discoveryEndpoint)
    .withEnvironment("Oidc__ClientId", "sebt-portal")
    .withEnvironment("Oidc__ClientSecret", "sebt-portal-dev-secret")
    .withEnvironment("Oidc__CallbackRedirectUri", portalCallbackUrl)
    .withEnvironment("Oidc__StepUp__DiscoveryEndpoint", discoveryEndpoint)
    .withEnvironment("Oidc__StepUp__ClientId", "sebt-portal-stepup")
    .withEnvironment("Oidc__StepUp__ClientSecret", "sebt-portal-stepup-dev-secret")
    // Step 3 of the guide. A phone override left over from another flow loads a household
    // that does not belong to the signed-in user, which reads as a data bug rather than a
    // configuration one.
    .withEnvironment("DevelopmentPhoneOverride__Phone", "")
    .waitFor(keycloak);

  return { redis, keycloak };
}
