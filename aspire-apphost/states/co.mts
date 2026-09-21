// Resources that are specific to CO. Redis is here and not in shared.mts because the CO
// appsettings is the one that declares a Redis section. Redis holds the CBMS household
// cache. DC uses HybridCache with level 1 only.
//
// Two other parts of CO are capabilities, and they are not here:
//   ../capabilities/sign-in-co.mts          Keycloak, the OIDC provider.
//   ../capabilities/household-source-co.mts the 2 mock switches for CBMS.

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
    .waitFor(redis);

  return { redis };
}
