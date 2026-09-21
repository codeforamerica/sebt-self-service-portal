// Resources that are specific to CO. Redis is here and not in shared.mts because the CO
// appsettings is the one that declares a Redis section. Redis holds the CBMS household
// cache. DC uses HybridCache with level 1 only.
//
// Guardians sign in against an OIDC provider. Keycloak is not here. It belongs to the
// sign-in capability, in ../capabilities/sign-in-co.mts.

import { EndpointProperty } from "../.aspire/modules/aspire.mjs";
import type {
  DistributedApplicationBuilder,
  ProjectResource,
  RedisResource,
} from "../.aspire/modules/aspire.mjs";
import type { AppHostConfig } from "../config.mjs";

export interface CoResources {
  /** Distributed cache that holds the CBMS household cache. */
  redis: RedisResource;
}

export async function addCoResources(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  api: ProjectResource,
): Promise<CoResources> {
  // We give this value, and we do not let Aspire make one. Thus the API can get the
  // value, and a person can use it with redis-cli.
  const redisPassword = await builder.addParameter("redis-password", {
    value: config.redisPassword,
    secret: true,
  });

  // TLS is the same as in compose, which serves Redis with TLS to agree with the
  // in-transit encryption of ElastiCache. Aspire ends the TLS with the developer
  // certificate. It gives a plain endpoint and a `rediss://` endpoint. A person must run
  // `aspire certs trust` one time on each machine. Without a trusted certificate, Aspire
  // serves the plain endpoint only, and it shows no message.
  //
  // That certificate is trusted. Thus the API does a check of it, and it does not bypass
  // the CA trust. For this reason, AcceptSelfSignedCertificates stays false below.
  const redis = await builder
    .addRedis("redis", { password: redisPassword })
    // This is explicit, but Aspire applies it to a Redis container by default.
    .withHttpsDeveloperCertificate()
    // The 2 user interfaces run as child resources. They replace the redis-commander
    // service of compose.
    .withRedisCommander()
    .withRedisInsight();

  // The endpoint with the name `tcp` is the TLS endpoint. The plain port has the name
  // `secondary`. Aspire makes TLS the primary endpoint when a certificate is present.
  const endpoint = await redis.getEndpoint("tcp");

  await api
    .withEnvironment("Redis__Host", await endpoint.property(EndpointProperty.Host))
    .withEnvironment("Redis__Port", await endpoint.property(EndpointProperty.Port))
    .withEnvironment("Redis__Ssl", "true")
    // The developer certificate is for localhost. Thus localhost is the name to check.
    .withEnvironment("Redis__SslHost", "localhost")
    .withEnvironment("Redis__AcceptSelfSignedCertificates", "false")
    .withEnvironment("Redis__Password", redisPassword)
    // Step 4 of docs/development/keycloak-oidc.md. The fixture users of the realm get a
    // household only when the seed makes the addresses that they sign in with. Thus a
    // person cannot sign in against Keycloak and read the real CBMS data at the same
    // time. This graph selects Keycloak. ../capabilities/sign-in-co.mts gives the email
    // pattern that connects the 2 halves.
    .withEnvironment("Seeding__State", "co")
    .withEnvironment("UseMockHouseholdData", "true")
    // There are 2 mock switches, and both are necessary. UseMockHouseholdData above
    // belongs to the portal. It sends the household reads and writes to
    // MockHouseholdRepository. The switch below belongs to the CO connector. That
    // connector makes its own CBMS HTTP client, and it needs Cbms:ClientId and
    // Cbms:ClientSecret. Without the switch, the co-cbms-api-ping health check of the
    // connector reports Degraded on a checkout that has no credentials.
    .withEnvironment("Cbms__UseMockResponses", "true")
    .waitFor(redis);

  return { redis };
}
