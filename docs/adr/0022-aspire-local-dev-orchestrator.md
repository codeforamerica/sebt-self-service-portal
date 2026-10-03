# 22. Adopt Aspire as the orchestrator for local development, with conditions

Date: 2026-09-02

## Status

Accepted, with the conditions in the decision below. It is the result of the spike DC-713.

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

Therefore the launch command selects the state. The commands are `pnpm aspire:dc` and `pnpm aspire:co`. All other values come from one file, `aspire-apphost/config.mts`. This file is the only module that reads `process.env`. To add a state, add one provider to each capability directory. There is no switch on the state.

### Infrastructure that differs by state is a capability, and a capability returns its requirements

The first version of the AppHost had a problem. Each state module received the `api` resource and changed it. The environment of the API was then the sum of the edits of 4 modules, in an order that mattered but that no module declared. A comment in `states/apps.mts` gave the rule, because nothing else did.

Therefore a capability provider does not change the API. It makes the resources that it owns. Then it returns what those resources require: the settings the API must receive, the resources the API must wait for, and the obligations of the host machine. The file `compose.mts` discharges them at one place, and `apphost.mts` holds no wiring. The types are in `aspire-apphost/capabilities/requirements.mts`.

This makes the configuration that the application needs into data. The AppHost prints it at each start, and a later change can make a comparison of it with the values that `appsettings`, `tofu`, and `web.config` give to the other environments.

The contract is the same in kind for each state, and not in content. DC and CO answer the sign-in question at different layers. The DC API makes and checks an email OTP itself, so it needs only a transport for mail. CO gives identity to an external provider, so it needs an IdP and a client registration. A common shape such as `{ host, port }` or `{ issuer, clientId }` would be a fiction, and the third state would break it.

Each of the 4 is now a capability. No state has a module of its own.

| Capability | DC | CO |
| --- | --- | --- |
| Sign-in | email OTP, with Mailpit; ID proofing against the Socure stub | OIDC, with Keycloak |
| Household source | the `DcSource` database and a seed job | mock CBMS |
| Connector build | a build of the second repository | a build of the in-repo project |
| Cache | none, `HybridCache` level 1 only | Redis with TLS |

The cache of DC is an empty provider. It makes no resource, it gives no setting, and it needs no wait. This is intentional. An empty provider says that DC answered the question, and a missing provider is an error of compilation. A person who reads the matrix sees both answers.

One directory holds each capability, and each directory has the same 3 files. Therefore the layout on disk agrees with the matrix above: one row is one directory, and one column is one file.

```
capabilities/
  requirements.mts              the shape that each capability speaks in
  cache/contract.mts            the provider type and the map of the states
  cache/dc.mts                  the answer of DC
  cache/co.mts                  the answer of CO
  connector-build/{contract,dc,co}.mts
  household-source/{contract,dc,co}.mts
  sign-in/{contract,dc,co}.mts
```

To compare 2 states, read the 2 files in one directory. To add a state, add one file to each directory.

The AppHost prints the full contract at each start. This is the output for CO:

```
[cache] Redis with TLS. ...
[connector-build] a build of the CO plugin project in this repository. ...
[household-source] mock CBMS, in the process of the API. ...
[sign-in] OIDC via Keycloak. ...
[preflight] cache: a trusted HTTPS developer certificate
[preflight] connector-build: CO plugin project at .../SEBT.Portal.StatePlugins.CO.csproj
[preflight] sign-in: a trusted HTTPS developer certificate
[preflight] sign-in: Keycloak realm import file at .../sebt-realm.json
[preflight] sign-in: Keycloak login theme at .../themes
[config] cache supplies api: Redis__Host, Redis__Port, Redis__Ssl, Redis__SslHost,
         Redis__AcceptSelfSignedCertificates, Redis__Password
[config] cache waits for: redis (healthy)
[config] connector-build supplies api: PluginAssemblyPaths__0
[config] connector-build waits for: co-plugin-build (completion)
[config] household-source supplies api: UseMockHouseholdData, Cbms__UseMockResponses, Seeding__State
[config] sign-in supplies api: Oidc__DiscoveryEndpoint, Oidc__ClientId, ... Seeding__EmailPattern
[config] sign-in neutralizes api: DevelopmentPhoneOverride__Phone, Seeding__CoLoadedSeedEmailOverride
[config] sign-in waits for: keycloak (healthy)
[config] sign-in portal binding supplies api: Oidc__CallbackRedirectUri
[config] sign-in portal binding supplies web: OIDC_ISSUER_ORIGIN
```

This answers the question that made us start this work: which configuration does this state need to run? Before, the answer was in the comments of 4 modules, and each module changed the API itself.

The household source found a second gap, in the graph of DC. The DC connector calls 4 stored procedures, and it has no compiled-in default for a name, because the schema differs for each environment. The graph gave the connection string and no procedure name. Thus `DcSummerEbtCaseService` threw an `InvalidOperationException` for each household lookup, and `DcAddressUpdateService` gave the result `NOT_CONFIGURED`. The graph now gives each of the 4 names:

| Setting | Value | The script that makes it |
| --- | --- | --- |
| `DCConnector__GetHouseholdByGuardianProcName` | `[sebt_app_test].[GetHouseholdByGuardian]` | 102_proc_GetHouseholdByGuardian.sql |
| `DCConnector__AddressUpdateProcName` | `[dbo].[UpdateMailingAddress]` | 103_proc_UpdateMailingAddress.sql |
| `DCConnector__CardReplacementProcName` | `[sebt_app_test].[RequestNewCard]` | 104_proc_RequestNewCard.sql |
| `DCConnector__CheckEligibilityProcName` | `dbo.sp_CheckEligibility` | 105_proc_CheckEligibility.sql |

A query of the seeded database confirms each of the 4 names, and the household lookup gives a household of 3 people for the guardian `test0001@email.com`.

DC gave us the body of `[sebt_app_test].[RequestNewCard]` from `ESA_LINK`, and 104_proc_RequestNewCard.sql now holds it. The changes are the same 3 that 102 lists: no `USE [ESA_LINK]` line, `CREATE OR ALTER PROCEDURE`, and a guard on the CREATE TABLE for `RequestCardLog`. The body holds `@IsTest = 1`, so each call answers `@resultCode = 0`, and `DcCardReplacementService` reads that as a success. A test confirms this, and it confirms that a second run of the script gives no error.

105_proc_CheckEligibility.sql holds `dbo.sp_CheckEligibility`, from `src/Scripts/Database/Procedures/sp_CheckEligibility.sql` of codeforamerica/sebt-dc-enrollment-checker at 6299229. The connector is the source of the signature, and the script follows the connector. The enrollment checker declares `@mailingAddressStreetLine1`, `@mailingAddressStreetLine2`, `@mailingAddressCity`, `@mailingAddressState`, and `@mailingAddressZipCode`. `DcEnrollmentCheckService` binds `@addressLine1`, `@addressLine2`, `@city`, `@state`, and `@zip`. `CommandType.StoredProcedure` sends each parameter by name, so a procedure with the other names gives the error "@addressLine1 is not a parameter for procedure sp_CheckEligibility".

The body is the body of the enrollment checker, and `RAND()` decides the answer. Thus a child is eligible about half of the time, and a second call for the same child can answer differently. A rule on `@formData` in place of the first `SET` gives a repeatable answer. The connector reads the OUTPUT parameters only, so it ignores the result set of candidate matches.

The README also gives `[sebt_app_test].[UpdateMailingAddress]`, and the local script makes `[dbo].[UpdateMailingAddress]`. The header of that script calls it the local implementation. Therefore the 2 names are both correct, each one for its own environment.

The connector build found a fault in the graph of CO. The csproj of the API has no reference to the CO connector. Thus a build of the API alone stages no plugin, and `plugins-co` held only what an earlier `pnpm api:build-co` put there. A new checkout started the API with no CO connector, and nothing gave a message. The capability now gives CO a build job, the same as DC. A test confirms this: after a delete of `plugins-co`, the job made the directory again with 37 files, and the health check `co-cbms-api-ping` of the connector reported Healthy.

`states/dc.mts` is now empty, and it is deleted. DC is 3 capabilities and nothing else.

The second capability showed one fault in the shape of the first. The list of preflight checks was a fixed array. The paths of the DC household source come from `DC_CONNECTOR_PATH`, and only the resolved configuration knows that value. Therefore `preflight` is now a function of `AppHostConfig`, and both capabilities use that shape.

The household source also shows the 2 limits of the shape. The provider of DC makes 3 resources, gives 1 setting, and makes the API wait for the completion of the seed job. The provider of CO makes no resource and gives 3 settings. One contract holds both.

The map of the providers is `Record<SupportedState, SignInProvider>`. Therefore a new state in `SupportedState` is an error of compilation until that state has a sign-in flow.

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
| `pnpm dev:co-enroll` / `dev:dc-enroll` as a separate command | The checker is part of every state's graph                            |
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
  5. Aspire gives Keycloak a developer certificate and serves it with TLS. The endpoint keeps the name `http`, and no endpoint has the name `https`. Aspire publishes container port 8443 only, and it binds the fixed host port 8180 to that port. Keycloak also listens on 8080 in the container, but nothing publishes that port. An address of `http://localhost:8180` therefore accepts a TCP connection and answers nothing. The API cannot read the discovery document, `OidcController.Authorize` catches the error, and it sends the person back to `/login` with the log line `reason=discovery_failed`. Keycloak reported healthy for the whole time, because the health check of Aspire uses the management port. The sign-in provider now uses `https` and adds a health check that reads the discovery document.
  6. Two checkouts of this repository use the same name for the data volume, `sebt-portal-mssql-data`, and `withPersistentLifetime()` keeps the container after the session ends. The second checkout cannot start its own `mssql`. The container stops with `BootstrapSystemDataDirectories() failure`, and `portal-db` shows `Exited` while the API shows `Waiting`. This reads as a fault in the database, and not as a collision.
     Result: make a positive test of each statement about this stack. Do not look at the dashboard and assume.
- **The telemetry needs 2 explicit values.** The dashboard shows the console log of each resource with no extra work. For the structured logs, set `Otel:UseLogExporter` to `otlp`. For the traces and the metrics, set `Otel__OtlpExporter__Endpoint`. The AppHost now sets both values, so the result is the same for each developer. We made a test of the structured logs of the API, and they arrive in the dashboard. We did not make a test of the traces and the metrics.
- **The files `appsettings.{state}.json` and the `.env` files are still necessary.** Aspire does not remove this step from the setup. Aspire does not read a `.env` file. Compose reads it. If `appsettings.dc.json` is absent, the API stops with the message `PluginAssemblyPaths missing from configuration`. This is a problem in the local setup, not a problem in Aspire. The command `pnpm dev:dc` has the same result.
- **New tools are necessary.** A developer must install the Aspire CLI. The install goes through pnpm, `pnpm add -g @microsoft/aspire-cli@<version>`, because pnpm is already a prerequisite of this repository and the version then stays with `sdk.version` in `aspire.config.json`. The shell installer at aspire.dev and `dotnet tool install` are the other 2 methods, and each one adds a tool chain that the repository does not use for anything else. A developer must also run `aspire certs trust` one time on each machine. That command needs a person, so CI cannot run it. `scripts/dev/init-workspace.sh` now does both steps, and it asks first, so the cost of these 2 tools falls on the first run of that script and not on each developer in turn.
- **The versions change quickly.** During the spike, the CLI changed from 13.5.0 to 13.5.2, and then to 13.5.3. The SDK stayed at 13.5.0. Therefore each run showed a warning. Also, `aspire integration search` showed packages at version 13.5.1 that were not on nuget.org. The CLI has a catalog that is newer than the feed. The command `aspire update` now holds the SDK and each package at 13.5.4, which agrees with the CLI. Expect this problem again after the next release of the CLI.
- Aspire selects the host ports for each run. Therefore a developer must read the port from the dashboard. A saved link does not work.
- The data volumes are new. The data in the Compose `SebtPortal` database does not move to them.
- 2 MSSQL containers use about 1 GB more memory. This is a decision that we made, because it keeps the boundary that `DcSource` simulates.
- The topology has 2 declarations, Aspire and Compose, while both paths are available.

### Follow-ups

- **Correct the hot reload problem before the AppHost becomes the default path.** There are 2 options. Make the API an executable resource that runs `dotnet watch run`. This keeps the TypeScript AppHost, but it loses the functions of `addProject`, such as the endpoint from the launch profile and `aspire resource api rebuild`. The other option is an AppHost in C#. This gives full watch support, but we lose the TypeScript AppHost. Write a ticket for this work.
- **Done.** The AppHost sets `Otel__UseLogExporter=otlp`, so the result is the same for each developer. Make a test of the traces and the metrics in the dashboard.
- **Done.** The directory `states/` is now `shared/`, because no state owns what is in it. `states/shared.mts` is `shared/database.mts`, and `SharedResources` is `PortalDatabase`.
- **Make a check that each capability is discharged.** `Record<SupportedState, Provider>` makes a check that each state has a provider for each capability. Nothing makes a check that `compose.mts` calls `applyRequirements` for each one. A provider that nobody calls is silent: the graph starts, and the API misses one setting.

  We wrote this test and then removed it, to keep the spike small. The method is decided, and a later ticket can put it back. These are the results, so that nobody does the work again:

  `DistributedApplicationTestingBuilder` is the usual way, and it is not available to us. It needs a type from the `Projects` namespace, and that namespace holds an entry for each `<ProjectReference>` of a test project. This AppHost is TypeScript, so it has no csproj and no such entry. Read https://aspire.dev/testing/advanced-scenarios/.

  The command `aspire do publish-manifest --output-path <file>` gives the model in the one form that Aspire writes for a program. It runs the real AppHost with the real builder, it starts no container, and it takes about 6 seconds for each state. The JSON holds each resource and its environment, with each reference resolved, such as `{mailpit.bindings.smtp.host}` and `https://localhost:{keycloak.bindings.http.port}/realms/sebt/...`. Therefore a check reads the value and not the key only.

  The wiring is in `compose.mts` and not in `apphost.mts`, which also lets a test call `composeGraph` with a builder of its own.

  2 mutations showed that the check has value. With the call to `applyRequirements` for the cache removed, it failed. With the scheme of the Keycloak discovery document changed back to `http`, it failed and named the scheme. That second one is silent failure 5 above, and the check found it in 13 seconds.

  The manifest does not hold the waits. Therefore no check of that kind covers `waitFor`, and the line `[config] ... waits for:` at each start is the record of them.
- **Done.** The preflight of the cache and the preflight of sign-in both make a check of the trusted developer certificate. `capabilities/developer-certificate.mts` runs `dotnet dev-certs https --check --trust` one time for each start. The thumbprint in its output agrees with the file name in `KC_HTTPS_CERTIFICATE_FILE` of the Keycloak container, so this is the same certificate that Aspire mounts.
- **Add the lint and the build of the AppHost to CI.** Both are local commands today, so a change that does not compile can merge. The job runs `pnpm run aspire:lint` and `pnpm run aspire:build`. Neither one needs Docker. Add `aspire:test` to the same job when the check above comes back.
- Keep Redis in the cache capability, `capabilities/cache/co.mts`. If DC production moves from IIS to the container path with ElastiCache, move Redis to the shared path at that time.
- **Done.** `capabilities/sign-in/co.mts` uses `addKeycloak` with `withRealmImport`. A bind mount gives the themes from `docker/keycloak`. The realm reads the portal address from the variable `SEBT_PORTAL_ORIGIN`, so the portal keeps a port that Aspire selects. This replaces the Compose profile `keycloak` for the Aspire path. Read [ADR-0019](./0019-keycloak-local-oidc-stand-in.md).
- **Done.** The README has the section "Local development with Aspire". It gives the installation of the CLI, the command `aspire certs trust`, and the step for the `appsettings` files.
- **No workflow in CI makes a check of the AppHost.** `tsc` and `eslint` run only on a local machine. Therefore a change that does not compile can merge. Add a CI job that runs the lint and `pnpm aspire:build`.
- **Make 3 changes to `compose.yaml`, and do this for each result of the decision.** Use a fixed version for the Mailpit image. Add a health check for each database. Add a password for Redis, to agree with ElastiCache. This spike found these 3 problems in Compose. They are not functions of Aspire.
- Make CI test the TLS configuration and the telemetry. Do not assume them, because Aspire fails quietly.
- **Examine deployment in a different spike.** This is not in the scope of this ADR. Aspire can make deployment files with `aspire deploy` for Docker, K8s, ACA, and AWS. Our path uses ECR, IIS, and AWS with GitHub Actions.
- Examine the Aspire agent skills in this repository: `aspire`, `aspire-init`, `aspire-monitoring`, `aspire-orchestration`, `aspireify`, and `dotnet-inspect`. Make sure that they agree with our standards for code.

## References

- DC-713, the spike for the orchestrator for local development
- `aspire-apphost/apphost.mts`, `aspire-apphost/compose.mts`, `aspire-apphost/config.mts`, and `aspire-apphost/shared/{apps,database}.mts`
- `aspire-apphost/test/compose.test.mts` and `aspire-apphost/test/recording-builder.mts`
- `aspire-apphost/capabilities/requirements.mts`, and `aspire-apphost/capabilities/{cache,connector-build,household-source,sign-in}/{contract,dc,co}.mts`
- `aspire.config.json`, which holds the SDK version, the package versions, and the dashboard profile
- The root `package.json` and `aspire-apphost/package.json`, which hold `aspire:dc`, `aspire:co`, `aspire:stop`, and `aspire:status`
- [ADR-0007, the approach for the plugins of the states](./0007-multi-state-plugin-approach.md)
- [ADR-0017, the consolidation of the monorepo](./0017-monorepo-consolidation.md)
- [ADR-0019, Keycloak as the local OIDC stand-in](./0019-keycloak-local-oidc-stand-in.md)
- Aspire documentation: <https://aspire.dev/get-started/add-aspire-existing-app/>, <https://aspire.dev/app-host/hot-reload-and-watch/>, <https://aspire.dev/app-host/typescript-apphost/>
