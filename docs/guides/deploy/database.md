---
description: The three ways to create the portal's schema in your SQL Server database, and how to pick one.
keywords: database preparation schema migrations EF Core Entity Framework DACPAC sqlpackage SQL script permissions dbcreator db_ddladmin db_owner __EFMigrationsHistory DBA install
---

# Prepare the database

The portal defines its own schema with Entity Framework Core migrations. Before the API can serve traffic, that
schema has to exist in your SQL Server database.

There are three ways to put it there. They differ in who runs the change and what permissions the application needs,
not in the schema you end up with. Pick the one that matches how your organization handles database changes.

## How the schema is applied

On start-up the API runs every migration that has not been applied yet. Entity Framework Core records what it has
run in an `__EFMigrationsHistory` table and skips anything already listed there, so starting against a database that
is already current does nothing.

> [!IMPORTANT]
> A failed migration does not stop the application. The start-up path catches the exception, logs it, and continues
> to serve traffic. The health check does not catch this either: it opens a connection and runs `SELECT 1`, so it
> reports healthy against a database that has no tables at all. A permissions problem therefore looks like a clean
> install until the first request touches the database. Always check the API log after an install, as described in
> [Confirm it worked](#confirm-it-worked).

## Option 1: let the application apply migrations

The simplest path, and the right one when the team running the portal also owns the database.

The application's SQL login needs enough rights to change the schema:

- If the database does not exist yet, Entity Framework Core creates it. The login needs the `dbcreator` server role,
  or explicit `CREATE DATABASE` permission.
- If you create an empty database first, the login only needs rights inside it. `db_owner` covers everything.
  A narrower grant is `db_ddladmin`, plus `db_datareader` and `db_datawriter` for normal operation.
- Either way, Entity Framework Core creates and writes `__EFMigrationsHistory` itself. It does not need to be
  created for it.

Creating the database yourself and granting only the narrower set is worth the extra step. It keeps the application
from being able to create or drop databases on the server.

Note that every release can add migrations, so the login keeps needing DDL rights for as long as you use this
option. If that is not acceptable, use one of the other two.

## Option 2: apply a DACPAC

This is how Washington, DC installs and upgrades. It suits an environment where a database administrator applies all
schema changes and wants to review them first.

Every IIS release bundle carries a DACPAC alongside the application files, plus `deploy-report.html`,
`deploy-report.xml`, and `CHANGELOG-DACPAC.md` describing what the release changes.

The DACPAC is built by applying every migration to a throwaway database and extracting the result, including the
`__EFMigrationsHistory` rows. Because the migration history travels with the schema, the application's start-up
check sees the database as current and applies nothing. That means **the application login never needs DDL rights on
this path**, only read and write.

A database administrator reviews the change and then applies it. First the pre-flight report:

```
sqlpackage /Action:DeployReport ^
  /SourceFile:<dacpac> ^
  /TargetConnectionString:"<target connection string>" ^
  /OutputPath:pre-flight-report.xml
```

Then, once approved, the publish:

```
sqlpackage /Action:Publish ^
  /SourceFile:<dacpac> ^
  /TargetConnectionString:"<target connection string>"
```

Apply the database change before switching traffic to the new application version, and keep the previous version
available until the release is accepted.

## Option 3: apply a SQL script

Use this when a database administrator applies the change but your process wants reviewable SQL rather than a
DACPAC, or when the target is not reachable from any machine that can run `sqlpackage`.

Generate the script from the repository:

```
dotnet ef migrations script --idempotent \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj \
  --output sebt-portal-schema.sql
```

Both `--project` and `--startup-project` are required. The migrations live in the Infrastructure project, and the
configuration Entity Framework Core needs to build the model lives in the API project.

`--idempotent` wraps each migration in a check against `__EFMigrationsHistory`, so the script is safe to run against
a database at any migration level, including one that is already current. Without it, the script assumes a specific
starting point and fails anywhere else.

To produce a delta between two releases rather than the whole history, pass `--from` and `--to` with migration
names. See
[Applying migrations with SQL scripts](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#sql-scripts)
in the Entity Framework Core documentation for the full set of options.

As with the DACPAC, the script writes `__EFMigrationsHistory` as it goes, so the application sees a current database
on start-up and needs no DDL rights of its own.

## Confirm it worked

Whichever option you used, check the API log after the first start-up. A successful run logs:

```
Database migrations completed successfully
```

A failure logs an error beginning `Database migrations failed or database unavailable`, and the application keeps
running regardless. Treat that line as an install failure even though nothing crashed.

Do not rely on `/api/health` for this. Its database check only proves that the connection string works and the
server answers, not that any table exists.
