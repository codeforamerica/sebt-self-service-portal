// Resources shared by every state. State-specific resources live in ./dc.mts and
// ./co.mts, selected by ../apphost.mts.

import type {
  DistributedApplicationBuilder,
  ParameterResource,
  SqlServerDatabaseResource,
  SqlServerServerResource,
} from "../.aspire/modules/aspire.mjs";

/**
 * Local-dev SA password, matching compose.yaml and appsettings.json. Never used in a
 * deployed environment. Override by exporting MSSQL_SA_PASSWORD — the AppHost does not
 * read `.env` files the way Docker Compose does.
 */
const defaultSaPassword = "YourStrong@Passw0rd";

export interface SharedResources {
  /** SQL Server instance hosting the portal's own database. */
  sql: SqlServerServerResource;
  /** The portal's application database. EF Core migrations apply on API startup. */
  portalDb: SqlServerDatabaseResource;
  /** Reused by state modules that stand up their own SQL Server. */
  saPassword: ParameterResource;
}

export async function addSharedResources(
  builder: DistributedApplicationBuilder,
): Promise<SharedResources> {
  // Explicit rather than Aspire's generated password: the value must stay stable so the
  // persistent data volume keeps accepting it and external tooling connects unchanged.
  const saPassword = await builder.addParameter("sql-password", {
    value: process.env.MSSQL_SA_PASSWORD ?? defaultSaPassword,
    secret: true,
  });

  // Host ports are intentionally unpinned. Aspire reaches containers through a host-side
  // proxy, and a proxy that cannot bind fails silently: the resource still reports
  // healthy while that port serves something else, such as a running compose stack.
  // Consumers get the assigned port injected, and the dashboard shows it for DB tooling.
  const sql = await builder
    .addSqlServer("mssql", { password: saPassword })
    .withDataVolume({ name: "sebt-portal-mssql-data" })
    // Matches `docker compose up -d`: the container outlives the AppHost so local data
    // survives. SQL Server fixes the SA password at creation, so changing
    // MSSQL_SA_PASSWORD later requires removing the container.
    .withPersistentLifetime();

  // Kebab-case resource name for the dashboard; databaseName is what connection strings
  // and EF Core migrations target.
  const portalDb = await sql.addDatabase("portal-db", {
    databaseName: "SebtPortal",
  });

  return { sql, portalDb, saPassword };
}
