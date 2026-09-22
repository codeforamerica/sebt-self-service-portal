// The database of the portal. Each state uses it, and no state changes it. A capability
// that needs its own database makes one, and ../capabilities/household-source/dc.mts is
// the example.

import type {
  DistributedApplicationBuilder,
  ParameterResource,
  SqlServerDatabaseResource,
  SqlServerServerResource,
} from "../.aspire/modules/aspire.mjs";
import type { AppHostConfig } from "../config.mjs";

export interface PortalDatabase {
  /** SQL Server instance that holds the database of the portal. */
  sql: SqlServerServerResource;
  /** The application database of the portal. EF Core migrations apply when the API starts. */
  portalDb: SqlServerDatabaseResource;
  /** A capability that makes its own SQL Server uses this parameter again. */
  saPassword: ParameterResource;
}

export async function addPortalDatabase(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
): Promise<PortalDatabase> {
  // This password is explicit, and Aspire does not make one. The value must stay the
  // same. Thus the persistent data volume continues to accept it, and an external tool
  // connects with no change.
  const saPassword = await builder.addParameter("sql-password", {
    value: config.sqlPassword,
    secret: true,
  });

  // The host ports are not pinned, and this is intentional. Aspire gets to a container
  // through a proxy on the host. If that proxy cannot bind, it shows no message. The
  // resource reports healthy, but a different program answers on that port. An example
  // is a compose stack that runs. A consumer gets the port that Aspire selected, and the
  // dashboard shows the port for a database tool.
  const sql = await builder
    .addSqlServer("mssql", { password: saPassword })
    .withDataVolume({ name: "sebt-portal-mssql-data" })
    // This agrees with `docker compose up -d`. The container continues after the AppHost
    // stops, so the local data stays. SQL Server sets the SA password one time, when it
    // makes the container. Thus a new value of MSSQL_SA_PASSWORD needs a new container.
    .withPersistentLifetime();

  // The resource name is in kebab-case for the dashboard. Connection strings and EF Core
  // migrations use `databaseName`.
  const portalDb = await sql.addDatabase("portal-db", {
    databaseName: "SebtPortal",
  });

  return { sql, portalDb, saPassword };
}
