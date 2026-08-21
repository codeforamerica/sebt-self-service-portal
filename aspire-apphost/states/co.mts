// Resources specific to CO. Redis lives here rather than in shared.mts because only
// CO's appsettings declares a Redis section — it backs the CBMS household cache, while
// DC runs HybridCache L1-only.

import type {
  DistributedApplicationBuilder,
  RedisResource,
} from "../.aspire/modules/aspire.mjs";

export interface CoResources {
  /** Distributed cache backing the CBMS household cache. */
  redis: RedisResource;
}

export async function addCoResources(
  builder: DistributedApplicationBuilder,
): Promise<CoResources> {
  // TLS mirrors compose, which serves Redis over TLS to match Elasticache in-transit
  // encryption. Aspire terminates TLS with the developer certificate and exposes both a
  // plain and a `rediss://` endpoint. Requires `aspire certs trust` once per machine —
  // without a trusted certificate Aspire silently serves the plain endpoint only.
  //
  // Because that certificate is trusted, consumers validate it instead of bypassing CA
  // trust: Redis__Ssl=true, SslHost=localhost, AcceptSelfSignedCertificates=false.
  //
  // Aspire also starts Redis with `--requirepass`, so consumers must supply
  // Redis__Password rather than assuming unauthenticated access.
  const redis = await builder
    .addRedis("redis")
    // Explicit, though Aspire applies this to Redis containers by default.
    .withHttpsDeveloperCertificate()
    // Both UIs run as child resources, replacing compose's redis-commander service.
    .withRedisCommander()
    .withRedisInsight();

  // TODO(DC-713): add Keycloak, standing in for MyColorado OIDC. Realm import and themes
  // are bind-mounted from docker/keycloak.

  return { redis };
}
