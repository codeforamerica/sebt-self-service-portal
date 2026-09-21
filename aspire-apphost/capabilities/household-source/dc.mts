// DC reads household data from a `DcSource` database. That database is a stand-in for
// the ESA_LINK system of the state. A one-shot job puts the data in it, and the job
// comes from the connector repository.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { EndpointProperty, refExpr } from "../../.aspire/modules/aspire.mjs";
import type {
  HouseholdSourceCapability,
  HouseholdSourceContext,
  HouseholdSourceProvider,
} from "./contract.mjs";

/** The image that seeds the database. It is in the checkout of the connector. */
const seedDockerfile = "Dockerfile.seed";

/** The SQL scripts that the seed image runs. */
const seedScripts = "scripts/sql";

async function provision({
  builder,
  config,
  saPassword,
}: HouseholdSourceContext): Promise<HouseholdSourceCapability> {
  const connectorPath = config.dcConnectorPath;

  // This is a separate server, and not one more database on the instance of the portal.
  // `DcSource` is an external system of the state, and the portal does not own it. One
  // instance for both would remove that boundary.
  const dcSourceSql = await builder
    .addSqlServer("dc-source", { password: saPassword })
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
  // The wait below is `completion`, and not `healthy`. Compose can wait only for the
  // server to report healthy. It cannot wait for the data.
  const dcSourceSeed = await builder
    .addDockerfile("dc-source-seed", connectorPath, {
      dockerfilePath: seedDockerfile,
    })
    .withEnvironment("DB_HOST", refExpr`${dbHost},${dbPort}`)
    .withEnvironment("DB_USER", "sa")
    .withEnvironment("DB_PASSWORD", saPassword)
    .waitFor(dcSourceDb)
    // If this is absent, a complete one-shot job stays in the dashboard and looks like a
    // fault.
    .withHiddenOnCompletion();

  return {
    requirements: {
      settings: [
        {
          key: "DCConnector__ConnectionString",
          value: dcSourceDb,
          why: "Where the DC connector reads the households of the state.",
        },
      ],
      // The API starts after the data is in the database. Otherwise the first request
      // reads a table that is empty, which looks like a household that does not exist.
      waits: [{ resource: dcSourceSeed, until: "completion" }],
    },
  };
}

export const dcSourceDatabase: HouseholdSourceProvider = {
  name: "the DcSource database, with a seed job",
  dataHint:
    "Households come from the SQL scripts of the connector; the dc-source-seed job loads them at each start.",
  // config.mts makes a check of the checkout itself. These checks are for the 2 paths
  // in the checkout that this capability needs. Aspire gives an error from Docker for an
  // absent Dockerfile, but it names neither the connector nor DC_CONNECTOR_PATH.
  preflight: (config) => [
    {
      description: `DC connector seed image at ${resolve(config.dcConnectorPath, seedDockerfile)}`,
      check: () => {
        const path = resolve(config.dcConnectorPath, seedDockerfile);
        if (!existsSync(path)) {
          throw new Error(
            `DC connector seed image not found at '${path}'. The dc-source-seed job builds from it. Make a check of DC_CONNECTOR_PATH.`,
          );
        }
      },
    },
    {
      description: `DC connector seed scripts at ${resolve(config.dcConnectorPath, seedScripts)}`,
      check: () => {
        const path = resolve(config.dcConnectorPath, seedScripts);
        if (!existsSync(path)) {
          throw new Error(
            `DC connector seed scripts not found at '${path}'. The seed job runs them, and without them the DcSource database stays empty.`,
          );
        }
      },
    },
  ],
  provision,
};
