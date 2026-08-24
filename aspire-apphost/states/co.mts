// Resources specific to CO. Redis lives here rather than in shared.mts because only
// CO's appsettings declares a Redis section — it backs the CBMS household cache, while
// DC runs HybridCache L1-only.

import { EndpointProperty } from "../.aspire/modules/aspire.mjs";
import type {
  DistributedApplicationBuilder,
  ProjectResource,
  RedisResource,
} from "../.aspire/modules/aspire.mjs";

/** Local-dev Redis password. Aspire requires auth; compose runs Redis unauthenticated. */
const defaultRedisPassword = "LocalDevRedis1!";

export interface CoResources {
  /** Distributed cache backing the CBMS household cache. */
  redis: RedisResource;
}

export async function addCoResources(
  builder: DistributedApplicationBuilder,
  api: ProjectResource,
): Promise<CoResources> {
  // Supplied explicitly rather than letting Aspire generate it, so the value can be
  // handed to the API and used with redis-cli.
  const redisPassword = await builder.addParameter("redis-password", {
    value: process.env.REDIS_PASSWORD ?? defaultRedisPassword,
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

  // TODO(DC-713): add Keycloak, standing in for MyColorado OIDC. Realm import and themes
  // are bind-mounted from docker/keycloak.

  return { redis };
}
