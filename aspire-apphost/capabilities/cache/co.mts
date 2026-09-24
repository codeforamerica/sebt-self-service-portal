// CO keeps the CBMS household cache in Redis. The configuration of CO is the one that
// declares a `Redis` section.

import { EndpointProperty } from "../../.aspire/modules/aspire.mjs";
import { trustedDeveloperCertificate } from "../developer-certificate.mjs";
import type {
  CacheCapability,
  CacheContext,
  CacheProvider,
} from "./contract.mjs";

async function provision({
  builder,
  config,
}: CacheContext): Promise<CacheCapability> {
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
  // The sign-in capability of CO meets the same behavior with Keycloak.
  const endpoint = await redis.getEndpoint("tcp");

  return {
    requirements: {
      settings: [
        {
          key: "Redis__Host",
          value: await endpoint.property(EndpointProperty.Host),
          why: "Where the API reaches the cache.",
        },
        {
          key: "Redis__Port",
          value: await endpoint.property(EndpointProperty.Port),
          why: "Aspire allocates this port, so the API cannot use a fixed one.",
        },
        {
          key: "Redis__Ssl",
          value: "true",
          why: "Aspire ends the TLS with the developer certificate.",
        },
        {
          key: "Redis__SslHost",
          value: "localhost",
          why: "The developer certificate is for localhost. Thus localhost is the name to check.",
        },
        {
          key: "Redis__AcceptSelfSignedCertificates",
          value: "false",
          why: "The certificate is trusted, so the API checks it and does not bypass the CA trust.",
        },
        {
          key: "Redis__Password",
          value: redisPassword,
          why: "Aspire needs authentication for Redis. Compose runs Redis with no authentication.",
        },
      ],
      waits: [{ resource: redis, until: "healthy" }],
    },
  };
}

export const coRedis: CacheProvider = {
  name: "Redis with TLS",
  cacheHint:
    "Redis Commander and RedisInsight run as child resources of redis in the dashboard.",
  // Without a trusted certificate, Aspire serves the plain endpoint of Redis and it
  // shows no message. The API uses Redis__Ssl=true, so it then reaches nothing.
  preflight: () => [trustedDeveloperCertificate],
  provision,
};
