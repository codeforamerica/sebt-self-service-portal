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

// The 4 procedures that the DC connector calls. It has no default for a name, because
// the schema differs for each environment. These are the names that scripts/sql 102 to
// 105 make. Read docs/adr/0022-aspire-local-dev-orchestrator.md.
const householdLookupProcedure = "[sebt_app_test].[GetHouseholdByGuardian]";
const addressUpdateProcedure = "[dbo].[UpdateMailingAddress]";
const cardReplacementProcedure = "[sebt_app_test].[RequestNewCard]";
const checkEligibilityProcedure = "dbo.sp_CheckEligibility";

async function provision({
  builder,
  config,
  saPassword,
}: HouseholdSourceContext): Promise<HouseholdSourceCapability> {
  const connectorPath = config.dcConnectorPath;

  // A separate server, and not one more database on the portal instance. `DcSource` is
  // an external system that the portal does not own, and one instance would remove that
  // boundary.
  const dcSourceSql = await builder
    .addSqlServer("dc-source", { password: saPassword })
    .withDataVolume({ name: "sebt-dc-source-mssql-data" })
    .withPersistentLifetime();

  // 000_CreateDatabase.sql has an IF NOT EXISTS guard, so making it here is safe.
  const dcSourceDb = await dcSourceSql.addDatabase("dc-source-db", {
    databaseName: "DcSource",
  });

  // sqlcmd needs `host,port`. EndpointProperty.HostAndPort gives `host:port`.
  const dbEndpoint = await dcSourceSql.getEndpoint("tcp");
  const dbHost = await dbEndpoint.property(EndpointProperty.Host);
  const dbPort = await dbEndpoint.property(EndpointProperty.Port);

  // The seed image of the connector repository. seed-aws.sh does a TRUNCATE before it
  // seeds, so a second run gives the same data.
  const dcSourceSeed = await builder
    .addDockerfile("dc-source-seed", connectorPath, {
      dockerfilePath: seedDockerfile,
    })
    .withEnvironment("DB_HOST", refExpr`${dbHost},${dbPort}`)
    .withEnvironment("DB_USER", "sa")
    .withEnvironment("DB_PASSWORD", saPassword)
    .waitFor(dcSourceDb)
    // A complete one-shot job otherwise stays in the dashboard and looks like a fault.
    .withHiddenOnCompletion();

  return {
    requirements: {
      settings: [
        {
          key: "DCConnector__ConnectionString",
          value: dcSourceDb,
          why: "Where the DC connector reads the households of the state.",
        },
        {
          key: "DCConnector__GetHouseholdByGuardianProcName",
          value: householdLookupProcedure,
          why: "Reads a household for a guardian. Without it each lookup of DC throws.",
        },
        {
          key: "DCConnector__AddressUpdateProcName",
          value: addressUpdateProcedure,
          why: "Writes a new mailing address. Without it a change of address gives NOT_CONFIGURED.",
        },
        {
          key: "DCConnector__CardReplacementProcName",
          value: cardReplacementProcedure,
          why: "Asks for a new card. It answers SUCCESS for each call on a local machine.",
        },
        {
          key: "DCConnector__CheckEligibilityProcName",
          value: checkEligibilityProcedure,
          why: "Does a check of the eligibility of a child.",
        },
        // The 2 settings below connect the users of the portal to the rows of DcSource.
        // The seed scripts put addresses of this shape in PortalID, and
        // GetHouseholdByGuardian matches PortalID against the email of the person who
        // signed in. The default pattern is {0}@example.com, so without these the portal
        // has no user that any row names, and each household comes back empty.
        //
        // Seeding__State also decides whether DatabaseSeeder makes the DC scenarios at
        // all, and those are the users whose id proofing is complete. The stored
        // procedure matches an email only when isIdentityProofed is 1.
        {
          key: "Seeding__EmailPattern",
          value: "sebt.dc+{0}@codeforamerica.org",
          why: "Makes the seeded users have the addresses that the PortalID of a DcSource row holds.",
        },
        {
          key: "Seeding__State",
          value: "dc",
          why: "Tells the seed which state to make users for. The DC scenarios exist only under this value.",
        },
      ],
      // The API starts after the data lands. Otherwise the first request reads an empty
      // table, which looks like a household that does not exist.
      waits: [{ resource: dcSourceSeed, until: "completion" }],
    },
  };
}

export const dcSourceDatabase: HouseholdSourceProvider = {
  name: "the DcSource database, with a seed job",
  dataHint:
    "Households come from the SQL scripts of the connector; the dc-source-seed job loads them at each start.",
  // config.mts checks the checkout itself. Docker reports an absent Dockerfile, but it
  // names neither the connector nor DC_CONNECTOR_PATH.
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
