// Resources that are specific to DC. The portal reads household data from a separate
// `DcSource` database. This database is a stand-in for the ESA_LINK system of DC.
//
// Guardians sign in with an email OTP. The SMTP sink for that mail is not here. It
// belongs to the sign-in capability, in ../capabilities/sign-in-dc.mts.

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
  /** SQL Server that is a stand-in for the ESA_LINK system of DC. */
  dcSourceSql: SqlServerServerResource;
  /** The database that the DC connector reads household data from. */
  dcSourceDb: SqlServerDatabaseResource;
  /** One-shot seed job. Use `waitForCompletion` to make a consumer wait for it. */
  dcSourceSeed: ContainerResource;
  /** One-shot job that registers ~/nuget-store. The plugin build can restore from it. */
  nugetStore: ExecutableResource;
  /** One-shot build that puts the DC plugin DLLs into plugins-dc. */
  pluginBuild: ExecutableResource;
}

export async function addDcResources(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  shared: SharedResources,
  api: ProjectResource,
): Promise<DcResources> {
  const connectorPath = config.dcConnectorPath;

  // This is a separate server, and not one more database on the instance of the portal.
  // `DcSource` is an external system of the state, and the portal does not own it. One
  // instance for both would remove that boundary.
  const dcSourceSql = await builder
    .addSqlServer("dc-source", { password: shared.saPassword })
    .withDataVolume({ name: "sebt-dc-source-mssql-data" })
    .withPersistentLifetime();

  // 000_CreateDatabase.sql has an IF NOT EXISTS guard. Thus it is safe to make the
  // database here and to run the seed scripts after.
  const dcSourceDb = await dcSourceSql.addDatabase("dc-source-db", {
    databaseName: "DcSource",
  });

  // sqlcmd needs `host,port`. EndpointProperty.HostAndPort gives `host:port`.
  const dbEndpoint = await dcSourceSql.getEndpoint("tcp");
  const dbHost = await dbEndpoint.property(EndpointProperty.Host);
  const dbPort = await dbEndpoint.property(EndpointProperty.Port);

  // This uses the seed image of the connector repository, and it does not write that
  // script loop again. seed-aws.sh does a TRUNCATE of HouseholdCases before it seeds.
  // Thus a second run gives the same data, and it does not make more rows.
  //
  // `waitForCompletion` on this resource makes a consumer wait until the seed job is
  // complete. Compose can wait only for the server to report healthy. It cannot wait for
  // the data.
  const dcSourceSeed = await builder
    .addDockerfile("dc-source-seed", connectorPath, {
      dockerfilePath: "Dockerfile.seed",
    })
    .withEnvironment("DB_HOST", refExpr`${dbHost},${dbPort}`)
    .withEnvironment("DB_USER", "sa")
    .withEnvironment("DB_PASSWORD", shared.saPassword)
    .waitFor(dcSourceDb)
    // If this is absent, a complete one-shot job stays in the dashboard and looks like a
    // fault.
    .withHiddenOnCompletion();

  // The csproj of the connector uses a ProjectReference for the plugin contract when it
  // can see this repository as a sibling. If it cannot see it, the csproj uses the
  // SEBT.Portal.StatesPlugins.Interfaces package. This graph can get that second path.
  // DC_CONNECTOR_PATH can point to a checkout that is not a sibling. The connector finds
  // the contract path from its own location, and not from ours. Then the package must
  // come from ~/nuget-store.
  //
  // This runs the setup.sh of the connector, and it does not write that script again.
  // This is the same reason that dc-source-seed uses the Dockerfile.seed of the
  // connector. The script is idempotent. It makes the directory with `mkdir -p`, and it
  // changes the NuGet source if a source is present. The script writes to the global
  // NuGet configuration of the user. This is the one part of this graph that changes
  // data outside the two repositories.
  const nugetStore = await builder
    .addExecutable(
      "dc-nuget-store",
      resolve(connectorPath, "setup.sh"),
      connectorPath,
      [],
    )
    .withHiddenOnCompletion();

  // The DC connector builds outside this repository. Thus its plugin DLLs must go into
  // plugins-dc before the API loads the plugins at start. This job waits for the store
  // above. Then a restore that needs the package finds the source.
  //
  // This builds the plugin csproj, and not scripts/dev/build-dc.sh. That script also
  // builds the test project of the connector. That project gets SSH.NET through
  // Testcontainers.MsSql, which fails with NU1903. This occurs if the layout puts the
  // Directory.Build.props of the portal above the connector. The CopyPlugins target of
  // the plugin copies the DLLs in both cases.
  const pluginBuild = await builder
    .addExecutable("dc-plugin-build", "dotnet", connectorPath, [
      "build",
      resolve(
        connectorPath,
        "src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj",
      ),
      // If this property is absent, the connector finds the contract from its own
      // location. Then it uses the NuGet package when this repository is not its sibling.
      `-p:StateConnectorInterfacesProject=${resolve(
        repoRoot,
        "apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj",
      )}`,
      `-p:PluginDestDir=${resolve(
        repoRoot,
        `apps/portal/src/SEBT.Portal.Api/plugins-${config.state}`,
      )}`,
    ])
    .waitForCompletion(nugetStore)
    .withHiddenOnCompletion();

  await api
    .withEnvironment("DCConnector__ConnectionString", dcSourceDb)
    .waitForCompletion(dcSourceSeed)
    .waitForCompletion(pluginBuild);

  return { dcSourceSql, dcSourceDb, dcSourceSeed, nugetStore, pluginBuild };
}
