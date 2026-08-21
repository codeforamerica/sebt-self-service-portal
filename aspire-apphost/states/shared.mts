// Resources shared by every state. State-specific resources live in ./dc.mts and
// ./co.mts, selected by ../apphost.mts.

import type {
  DistributedApplicationBuilder,
  ParameterResource,
  RedisResource,
  SqlServerDatabaseResource,
  SqlServerServerResource,
} from '../.aspire/modules/aspire.mjs';

/**
 * Local-dev SA password, matching compose.yaml and appsettings.json. Never used in a
 * deployed environment. Override by exporting MSSQL_SA_PASSWORD — the AppHost does not
 * read `.env` files the way Docker Compose does.
 */
const defaultSaPassword = 'YourStrong@Passw0rd';

export interface SharedResources {
  /** SQL Server instance hosting the portal's own database. */
  sql: SqlServerServerResource;
  /** The portal's application database. EF Core migrations apply on API startup. */
  portalDb: SqlServerDatabaseResource;
  /** Distributed cache. Optional — without Redis config, HybridCache stays L1-only. */
  redis: RedisResource;
  /** Reused by state modules that stand up their own SQL Server. */
  saPassword: ParameterResource;
}

export async function addSharedResources(
  builder: DistributedApplicationBuilder,
): Promise<SharedResources> {
  // Explicit rather than Aspire's generated password: the value must stay stable so the
  // persistent data volume keeps accepting it and external tooling connects unchanged.
  const saPassword = await builder.addParameter('sql-password', {
    value: process.env.MSSQL_SA_PASSWORD ?? defaultSaPassword,
    secret: true,
  });

  // Host ports are intentionally unpinned. Aspire reaches containers through a host-side
  // proxy, and a proxy that cannot bind fails silently: the resource still reports
  // healthy while that port serves something else, such as a running compose stack.
  // Consumers get the assigned port injected, and the dashboard shows it for DB tooling.
  const sql = await builder
    .addSqlServer('mssql', { password: saPassword })
    .withDataVolume({ name: 'sebt-portal-mssql-data' })
    // Matches `docker compose up -d`: the container outlives the AppHost so local data
    // survives. SQL Server fixes the SA password at creation, so changing
    // MSSQL_SA_PASSWORD later requires removing the container.
    .withPersistentLifetime();

  // Kebab-case resource name for the dashboard; databaseName is what connection strings
  // and EF Core migrations target.
  const portalDb = await sql.addDatabase('portal-db', { databaseName: 'SebtPortal' });

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
    .addRedis('redis')
    // Explicit, though Aspire applies this to Redis containers by default.
    .withHttpsDeveloperCertificate()
    // Replaces compose's redis-commander service.
    .withRedisCommander();

  return { sql, portalDb, redis, saPassword };
}
