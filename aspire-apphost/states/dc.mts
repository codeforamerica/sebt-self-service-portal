// Resources specific to DC. Household data is read from a separate `DcSource` database
// standing in for the state's ESA_LINK system, and guardians authenticate by email OTP,
// so DC also needs a local SMTP sink.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { EndpointProperty, refExpr } from "../.aspire/modules/aspire.mjs";
import type {
  ContainerResource,
  DistributedApplicationBuilder,
  SqlServerDatabaseResource,
  SqlServerServerResource,
} from "../.aspire/modules/aspire.mjs";
import type { SharedResources } from "./shared.mjs";

/**
 * The DC connector is a separate repository, and its Dockerfile.seed plus scripts/sql
 * are the source of DcSource's schema and test data, so the checkout must be on disk.
 * Defaults to a sibling of this repo; override with DC_CONNECTOR_PATH.
 */
function resolveConnectorPath(): string {
  const path =
    process.env.DC_CONNECTOR_PATH ??
    resolve(
      import.meta.dirname,
      "../../..",
      "sebt-self-service-portal-dc-connector",
    );

  if (!existsSync(path)) {
    throw new Error(
      `DC connector checkout not found at '${path}'. Clone it beside this repo, or set DC_CONNECTOR_PATH.`,
    );
  }

  return path;
}

export interface DcResources {
  /** SQL Server standing in for DC's ESA_LINK system. */
  dcSourceSql: SqlServerServerResource;
  /** The database the DC connector reads household data from. */
  dcSourceDb: SqlServerDatabaseResource;
  /** One-shot seed job. Gate consumers on it with waitForCompletion. */
  dcSourceSeed: ContainerResource;
  /** SMTP sink for email OTP. */
  mailpit: ContainerResource;
}

export async function addDcResources(
  builder: DistributedApplicationBuilder,
  shared: SharedResources,
): Promise<DcResources> {
  const connectorPath = resolveConnectorPath();

  // A separate server rather than another database on the portal's instance: DcSource
  // represents an external state system the portal does not own, and collapsing the two
  // would erase that boundary.
  const dcSourceSql = await builder
    .addSqlServer("dc-source", { password: shared.saPassword })
    .withDataVolume({ name: "sebt-dc-source-mssql-data" })
    .withPersistentLifetime();

  // 000_CreateDatabase.sql is IF NOT EXISTS-guarded, so creating the database here and
  // letting the seed scripts run afterwards is safe.
  const dcSourceDb = await dcSourceSql.addDatabase("dc-source-db", {
    databaseName: "DcSource",
  });

  // sqlcmd expects `host,port`; EndpointProperty.HostAndPort renders `host:port`.
  const endpoint = await dcSourceSql.getEndpoint("tcp");
  const host = await endpoint.property(EndpointProperty.Host);
  const port = await endpoint.property(EndpointProperty.Port);

  // Reuses the connector repo's own seed image instead of reimplementing its script
  // loop. seed-aws.sh truncates HouseholdCases before reseeding, so repeat runs
  // converge rather than duplicating rows.
  //
  // waitForCompletion on this resource lets consumers block until seeding finishes.
  // Compose can only wait for the server to report healthy, not for the data to land.
  const dcSourceSeed = await builder
    .addDockerfile("dc-source-seed", connectorPath, {
      dockerfilePath: "Dockerfile.seed",
    })
    .withEnvironment("DB_HOST", refExpr`${host},${port}`)
    .withEnvironment("DB_USER", "sa")
    .withEnvironment("DB_PASSWORD", shared.saPassword)
    .waitFor(dcSourceDb)
    // A finished one-shot otherwise sits in the dashboard looking like a failure.
    .withHiddenOnCompletion();

  // Health check paths follow CommunityToolkit's MailPit integration. The image tag is
  // pinned; compose uses a floating `latest`.
  const mailpit = await builder
    .addContainer("mailpit", { image: "axllent/mailpit", tag: "v1.30.7" })
    .withEndpoint({ name: "smtp", targetPort: 1025, scheme: "smtp" })
    .withHttpEndpoint({ name: "http", targetPort: 8025 })
    .withEnvironment("MP_MAX_MESSAGES", "5000")
    .withHttpHealthCheck({
      path: "/livez",
      statusCode: 200,
      endpointName: "http",
    })
    .withHttpHealthCheck({
      path: "/readyz",
      statusCode: 200,
      endpointName: "http",
    });

  return { dcSourceSql, dcSourceDb, dcSourceSeed, mailpit };
}
