# 22. Adopt Aspire as the orchestrator for local development, with conditions

Date: 2026-09-02

## Status

Proposed. This is a draft. It is the result of the spike DC-713.

## Context

To start the stack on a local machine, a developer must do many steps. The steps are in 2 repositories. Nothing keeps the steps in agreement with each other.

These are the steps:

- Run `docker compose up -d`. This starts MSSQL, Redis, Mailpit, Jaeger, and redis-commander. Redis uses TLS with self-signed certificates. Keycloak is optional.
- Run `scripts/dev/gen-redis-certs.sh`. The root `predev` hook runs this script. The script makes the certificates for Redis.
- Run `concurrently`. This starts the API with `dotnet watch` on port 5280. It also starts the portal with `next dev` on port 3000. The enrollment checker starts from a different command on port 3001.
- Run `build-dc.sh` or `build-co.sh`. These scripts build the state connectors. They then copy the DLL files into the directory `plugins-{state}`. The API reads the plugins from this directory.
- In the DC connector repository, run a second `docker compose up -d`. This starts MSSQL on port 1434. It also starts an `mssql-init` container. That container reads the files in `scripts/sql` into the database `DcSource`.

Each layer declares the topology and the configuration again in its own format. The API uses `appsettings*.json`. The containers use `compose.yaml`. The web applications use `env.ts`.

2 problems come from this. Nothing coordinates the 2 Compose stacks. Also, nothing makes the API wait for the data in `DcSource`. The Compose condition `depends_on: service_healthy` waits for the server, but it does not wait for the data.

The ticket DC-713 asked 2 questions. Can Aspire 13.x describe this graph in one AppHost with one command? Must we adopt it?

## Decision

**Adopt Aspire, but with conditions.** Keep the AppHost as an additional path for local development. Keep `compose.yaml`. Do not make the AppHost the default path before we correct the problem with hot reload.

This spike decided the items that follow.

### The AppHost language is TypeScript

The file `apphost.mts` hosts the .NET API. It uses `addProject('api', '<path>/SEBT.Portal.Api.csproj')` with the path to the `.csproj` file. It does not use a `ProjectReference`. This works on version 13.5.0.

The 2 Next.js applications use `addNextJsApp` with `withPnpm()`. This runs `pnpm dev`. Therefore each package still runs its own `predev` hook. The hook makes the design tokens and the locale files. The documentation shows `addNextJsApp` as experimental, but it needed no special work.

### The graph is composed for one state, and it does not change while it runs

The variable `STATE` selects which resources exist. DC needs a `DcSource` database and Mailpit. CO needs Redis and, later, Keycloak. Aspire composes the graph one time when it starts.

Therefore the launch command selects the state. The commands are `pnpm aspire:dc` and `pnpm aspire:co`. All other values come from one file, `aspire-apphost/config.mts`. This file is the only module that reads `process.env`. To add a state, add one module `states/<state>.mts` and one arm to the switch.

### The plugin build becomes part of the AppHost

The script `build-dc.sh` becomes a resource that runs one time. Its name is `dc-plugin-build`. The API waits for it with `waitForCompletion`.

The variable `DC_CONNECTOR_PATH` gives the path to the DC checkout. The AppHost makes a check of this path. If the path is absent, the AppHost stops and shows a clear message.

This answers the open question about DC. DC is in 2 repositories. Aspire uses paths, so a checkout beside this repository works. But we must declare this rule and make a check of it. We must not assume it.

### Redis is a resource for CO only

Only the CO configuration declares a `Redis` section. Also, the DC connector uses no cache.

DC production runs on IIS, and that deployment has no Redis. The workflow `release-iis-dc.yaml` sets no Redis values. The template `scripts/ci/templates/web.config` also sets no Redis values.

Therefore DC production uses HybridCache with a memory cache only. DC production also uses `SqlDistributedSynchronizationProvider` for the distributed lock. A local DC graph without Redis agrees with DC production.

The code shows the same intent. `AddCaching` stops the application at startup if 3 conditions are true together: the environment is not Development, OIDC is configured, and Redis is absent. DC uses email OTP, not OIDC, so this check does not apply to DC. CO uses OIDC, so CO needs Redis.

One difference stays between the environments. The AWS environment `dev-dc` uses `modules/sebt_application`, and that module makes an ElastiCache instance. Therefore `dev-dc` has Redis, but DC production does not. This ADR follows DC production, because that is the target that we must match.

This gives a rule. The file `shared.mts` holds a dependency that all states use. A state module holds a dependency that only some states use. To make this decision, look at the production target of the state. Do not look at the AWS lower environment.

### Redis uses TLS, and it is nearer to production than Compose

Aspire uses the developer certificate for Redis TLS. It gives 2 endpoints: one plain endpoint, and one `rediss://` endpoint. Compose gives the same 2 ports, 6379 and 6380.

A trusted certificate gives an advantage. The API can make a check of the certificate. The value `AcceptSelfSignedCertificates` stays false. The Compose path does not make this check.

Production ElastiCache uses TLS and an authentication token. Aspire also uses a password for Redis by default. Of the 3 configurations, Aspire is the nearest to production. Compose is the different one.

### Jaeger and redis-commander

The Aspire path does not use Jaeger. The dashboard reads OTLP data. For Redis, add `.withRedisCommander()` and `.withRedisInsight()` to the Redis resource.

### What the AppHost replaces

| Today                                                        | Aspire                                                                |
| ------------------------------------------------------------ | --------------------------------------------------------------------- |
| `docker compose up -d mssql`                                 | `mssql` and `portal-db`. Aspire makes the database `SebtPortal`.      |
| `docker compose up -d mailpit`                               | `mailpit`, at version `v1.30.7`, for DC only                          |
| Compose `redis`, `gen-redis-certs.sh`, and the `predev` hook | `addRedis` and the developer certificate for TLS                      |
| Compose `redis-commander`                                    | `withRedisCommander()` and `withRedisInsight()`                       |
| Compose `jaeger`                                             | The Aspire dashboard, which reads OTLP data                           |
| The separate Compose stack in the DC connector repository    | `dc-source` and `dc-source-db`, in the same graph                     |
| Its `mssql-init` loop                                        | `dc-source-seed`, which uses the `Dockerfile.seed` of that repository |
| `pnpm api:build-dc` as a manual step                         | `dc-plugin-build`. The API waits for it.                              |
| `dev:kill-port`, which uses `lsof` and `kill`                | Aspire controls the processes                                         |
| `concurrently -n API,Web`                                    | The resource graph                                                    |
| `pnpm dev:co-enroll` as a separate command                   | The checker is part of the CO graph                                   |
| `docker compose logs -f` and `docker compose down`           | The dashboard, `aspire logs <resource>`, and `aspire stop`            |

For DC, the daily start changes from 3 commands in 2 directories to 1 command.

### Alternatives considered

- **An AppHost in C#.** This gives `dotnet watch` for the .NET projects. The TypeScript AppHost cannot do this. We did not reject this alternative. We delayed it. Read the follow-ups.
- **One graph with all the resources of both states, and `withExplicitStart()` for the resources that must not start.** We rejected this. It gives one dashboard, but a DC developer always sees the CO resources, and a CO developer always sees the DC resources.
- **A parameter for `STATE` that a developer can change while the application runs.** We rejected this. It is not possible. A parameter configures a resource, but it cannot add a resource or remove one. Also, the TypeScript interface has no hook for a change to a parameter.
- **The package `CommunityToolkit.Aspire.Hosting.MailPit`.** We rejected this. It has no stable release. Each version is a prerelease. It uses an older image than we want. Its main advantage is a connection string through `withReference`, but we cannot use that advantage. Our SMTP keys are `SmtpClientSettings:*`, not `ConnectionStrings:*`. We use `addContainer` for Mailpit. We copied the endpoints and the health checks `/livez` and `/readyz` from that package.
- **Host ports with the same numbers as Compose.** We rejected this after a dangerous failure. Read the negative results.

## Consequences

### Positive

- One command starts a graph that includes 2 repositories. Before, the `DcSource` database was in the Compose file of a second repository, and nothing coordinated the 2 files.
- The API waits for the data. `waitForCompletion` holds the API until the seed job and the plugin build are complete. Compose cannot do this. Therefore the API can start today against a database with incomplete data. The API log shows the correct sequence: `Waiting for resource 'dc-source-seed' to complete`, then `Finished waiting`.
- Aspire makes a health check for each database, `portal-db` and `dc-source-db`. Compose makes a health check only for the server with `SELECT 1`.
- Each state starts only its own resources. CO does not start Mailpit or `DcSource`. DC does not start the 2 Redis user interfaces. Compose starts all of the services for each state.
- Aspire sends the infrastructure configuration to the API. This removes the local passwords and the port numbers from the local `appsettings` files.
- Redis uses TLS with a trusted certificate, and Redis needs a password. Both results are nearer to ElastiCache than the Compose path.
- The topology is code. `tsc` and `eslint` make a check of it. This found errors during the spike. Compose shows this type of error only when it runs.
- Aspire installs the dependencies of the 2 web applications. It uses a resource for each installation.
- A developer can control one resource with `aspire resource <name> stop`, `start`, or `rebuild`. The other resources continue to run.
- The image `mailpit:v1.30.7` has a fixed version. The Compose file uses the tag `latest`, which changes.
- CI does not change. No workflow uses Compose. Therefore this work adds a path, and it removes nothing.

### Negative results and trade-offs

- **The API loses hot reload.** This is the most important problem, because a developer edits C# code more often than any other action. We made a measurement. With the feature `defaultWatchEnabled` set to true, Aspire started the API with `dotnet run --configuration Release --no-launch-profile`. We then changed the time of the file `Program.cs`, and the API did not restart. The documentation gives `dotnet watch` for an AppHost in C#. A TypeScript AppHost watches only the AppHost files. The 2 Next.js applications keep hot reload, because their own development server gives it.
- **The API runs in the Release configuration**, not in Debug. The interface `ProjectResourceOptions` has no property to change this.
- **Aspire fails quietly. It does not show an error.** We found 4 examples in this spike:
  1. If the host proxy cannot use a port, Aspire shows the resource as healthy. But a different program answers on that port. The Compose container `sebt_mssql` used port 1433. Then `aspire describe` showed `mssql` as healthy on `tcp://localhost:1433`, but the Compose container answered. An EF migration can change the incorrect database. Aspire wrote no message. For this reason, the AppHost does not select the host ports.
  2. If no trusted developer certificate is available, Redis gives a plain endpoint. It does not give TLS. In a non-interactive session on macOS, the CLI cannot show the Keychain prompt. Therefore CI always has this result.
  3. The value `Otel:OtlpExporter:Endpoint` in `appsettings.json` has a higher priority than the standard variable `OTEL_EXPORTER_OTLP_ENDPOINT`. `withOtlpExporter()` sets that variable. Therefore the traces and the metrics go to the old Jaeger address, which we removed.
  4. The default value of `Otel:UseLogExporter` is `console`. Therefore the application registers no OpenTelemetry log provider, and Serilog uses `writeToProviders: false`. The structured logs in the dashboard stay empty until a developer changes this value.
     Result: make a positive test of each statement about this stack. Do not look at the dashboard and assume.
- **The telemetry needs 2 explicit values.** The dashboard shows the console log of each resource with no extra work. For the structured logs, set `Otel:UseLogExporter` to `otlp`. We made this change and we confirmed that the structured logs of the API arrive in the dashboard. For the traces and the metrics, the override of `Otel__OtlpExporter__Endpoint` is in the AppHost, but we did not make a test of it. Also, the value `Otel__UseLogExporter` is not yet in the repository, so each developer must set it again.
- **The files `appsettings.{state}.json` and the `.env` files are still necessary.** Aspire does not remove this step from the setup. Aspire does not read a `.env` file. Compose reads it. If `appsettings.dc.json` is absent, the API stops with the message `PluginAssemblyPaths missing from configuration`. This is a problem in the local setup, not a problem in Aspire. The command `pnpm dev:dc` has the same result.
- **New tools are necessary.** A developer must install the Aspire CLI. A developer must also run `aspire certs trust` one time on each machine. That command needs a person, so CI cannot run it.
- **The versions change quickly.** During the spike, the CLI changed from 13.5.0 to 13.5.2, and then to 13.5.3. The SDK stayed at 13.5.0. Therefore each run showed a warning. Also, `aspire integration search` showed packages at version 13.5.1 that were not on nuget.org. The CLI has a catalog that is newer than the feed.
- Aspire selects the host ports for each run. Therefore a developer must read the port from the dashboard. A saved link does not work.
- The data volumes are new. The data in the Compose `SebtPortal` database does not move to them.
- 2 MSSQL containers use about 1 GB more memory. This is a decision that we made, because it keeps the boundary that `DcSource` simulates.
- The topology has 2 declarations, Aspire and Compose, while both paths are available.

### Follow-ups

- **Correct the hot reload problem before the AppHost becomes the default path.** There are 2 options. Make the API an executable resource that runs `dotnet watch run`. This keeps the TypeScript AppHost, but it loses the functions of `addProject`, such as the endpoint from the launch profile and `aspire resource api rebuild`. The other option is an AppHost in C#. This gives full watch support, but we lose the TypeScript AppHost. Write a ticket for this work.
- Add `Otel__UseLogExporter=otlp` to the AppHost. The value is not in the repository now, so the result is not repeatable for other developers. Then make sure that the traces and the metrics also arrive at the dashboard.
- Keep Redis in `states/co.mts`. If DC production moves from IIS to the container path with ElastiCache, move Redis to the shared path at that time.
- Add Keycloak to `states/co.mts`. Use a bind mount for the realm file and the themes from `docker/keycloak`. This replaces the Compose profile `keycloak` for the Aspire path. Read [ADR-0019](./0019-keycloak-local-oidc-stand-in.md).
- Change the documentation for new developers. Add the installation of the Aspire CLI. Add `aspire certs trust`. Show that the step to copy the `appsettings` files is still necessary.
- **Make 3 changes to `compose.yaml`, and do this for each result of the decision.** Use a fixed version for the Mailpit image. Add a health check for each database. Add a password for Redis, to agree with ElastiCache. This spike found these 3 problems in Compose. They are not functions of Aspire.
- Make CI test the TLS configuration and the telemetry. Do not assume them, because Aspire fails quietly.
- **Examine deployment in a different spike.** This is not in the scope of this ADR. Aspire can make deployment files with `aspire deploy` for Docker, K8s, ACA, and AWS. Our path uses ECR, IIS, and AWS with GitHub Actions.
- Examine the Aspire agent skills in this repository: `aspire`, `aspire-init`, `aspire-monitoring`, `aspire-orchestration`, `aspireify`, and `dotnet-inspect`. Make sure that they agree with our standards for code.

## References

- DC-713, the spike for the orchestrator for local development
- `aspire-apphost/apphost.mts`, `aspire-apphost/config.mts`, and `aspire-apphost/states/{shared,dc,co,apps}.mts`
- `aspire.config.json`, which holds the SDK version, the package versions, and the dashboard profile
- The root `package.json` and `aspire-apphost/package.json`, which hold `aspire:dc`, `aspire:co`, `aspire:stop`, and `aspire:status`
- [ADR-0007, the approach for the plugins of the states](./0007-multi-state-plugin-approach.md)
- [ADR-0017, the consolidation of the monorepo](./0017-monorepo-consolidation.md)
- [ADR-0019, Keycloak as the local OIDC stand-in](./0019-keycloak-local-oidc-stand-in.md)
- Aspire documentation: <https://aspire.dev/get-started/add-aspire-existing-app/>, <https://aspire.dev/app-host/hot-reload-and-watch/>, <https://aspire.dev/app-host/typescript-apphost/>
