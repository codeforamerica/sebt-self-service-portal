// Resources specific to DC. Household data is read from a separate `DcSource` database
// standing in for the state's ESA_LINK system, and guardians authenticate by email OTP,
// so DC also needs a local SMTP sink.

import { resolve } from "node:path";

import { EndpointProperty, refExpr } from "../.aspire/modules/aspire.mjs";
import type {
  ContainerResource,
  DistributedApplicationBuilder,
  ExecutableResource,
  ProjectResource,
  SqlServerDatabaseResource,
  SqlServerServerResource,
} from "../.aspire/modules/aspire.mjs";
import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";
import type { SharedResources } from "./shared.mjs";

export interface DcResources {
  /** SQL Server standing in for DC's ESA_LINK system. */
  dcSourceSql: SqlServerServerResource;
  /** The database the DC connector reads household data from. */
  dcSourceDb: SqlServerDatabaseResource;
  /** One-shot seed job. Gate consumers on it with waitForCompletion. */
  dcSourceSeed: ContainerResource;
  /** One-shot registering ~/nuget-store, which the plugin build may restore from. */
  nugetStore: ExecutableResource;
  /** One-shot build staging the DC plugin DLLs into plugins-dc. */
  pluginBuild: ExecutableResource;
  /** SMTP sink for email OTP. */
  mailpit: ContainerResource;
}

export async function addDcResources(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  shared: SharedResources,
  api: ProjectResource,
): Promise<DcResources> {
  const connectorPath = config.dcConnectorPath;

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
  const dbEndpoint = await dcSourceSql.getEndpoint("tcp");
  const dbHost = await dbEndpoint.property(EndpointProperty.Host);
  const dbPort = await dbEndpoint.property(EndpointProperty.Port);

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
    .withEnvironment("DB_HOST", refExpr`${dbHost},${dbPort}`)
    .withEnvironment("DB_USER", "sa")
    .withEnvironment("DB_PASSWORD", shared.saPassword)
    .waitFor(dcSourceDb)
    // A finished one-shot otherwise sits in the dashboard looking like a failure.
    .withHiddenOnCompletion();

  // The connector's csproj resolves the plugin contract as a ProjectReference when it can
  // see this repo as a sibling, and falls back to the SEBT.Portal.StatesPlugins.Interfaces
  // package otherwise. That fallback is reachable from here: DC_CONNECTOR_PATH may point
  // at a checkout that is not a sibling, and the connector derives the contract path from
  // its own location, not from ours. The package then has to come from ~/nuget-store.
  //
  // Runs the connector's own setup.sh rather than reimplementing it, the same reasoning as
  // dc-source-seed reusing its Dockerfile.seed. The script is idempotent: it creates the
  // directory with mkdir -p and updates the NuGet source when one is already registered.
  // Note it writes to the user's global NuGet configuration, the only part of this graph
  // that touches state outside the two repositories.
  const nugetStore = await builder
    .addExecutable(
      "dc-nuget-store",
      resolve(connectorPath, "setup.sh"),
      connectorPath,
      [],
    )
    .withHiddenOnCompletion();

  // The DC connector builds out of tree, so its plugin DLLs must be staged into
  // plugins-dc before the API loads plugins at startup. Gated on the store above so a
  // restore that needs the package finds the source registered.
  const pluginBuild = await builder
    .addExecutable(
      "dc-plugin-build",
      resolve(repoRoot, "scripts/dev/build-dc.sh"),
      repoRoot,
      [],
    )
    .waitForCompletion(nugetStore)
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

  const smtpEndpoint = await mailpit.getEndpoint("smtp");

  await api
    .withEnvironment("DCConnector__ConnectionString", dcSourceDb)
    .withEnvironment(
      "SmtpClientSettings__SmtpServer",
      await smtpEndpoint.property(EndpointProperty.Host),
    )
    .withEnvironment(
      "SmtpClientSettings__SmtpPort",
      await smtpEndpoint.property(EndpointProperty.Port),
    )
    .waitForCompletion(dcSourceSeed)
    .waitForCompletion(pluginBuild)
    .waitFor(mailpit);

  return { dcSourceSql, dcSourceDb, dcSourceSeed, nugetStore, pluginBuild, mailpit };
}
