# CLAUDE.md

## Project Purpose

Plugin interface contracts (NuGet package) for the SEBT Self-Service Portal's multi-state plugin system. State connector implementations (DC, CO) depend on this package. See [README.md](./README.md) for full background and setup.

## Technology

- .NET 10, System.Composition (MEF)
- xUnit for tests
- C#: 4-space indent, Allman brace style, nullable reference types enabled

## Solution Structure

Solution file: `SEBT.Portal.StateConnector.slnx`

- `src/SEBT.Portal.StatesPlugins.Interfaces` -- interface contracts + shared models
- `src/SEBT.Portal.StatesPlugins.Interfaces.Tests` -- xUnit tests

### Key Interfaces

IStatePlugin (base marker), ISummerEbtCaseService, IEnrollmentCheckService, IAddressUpdateService, IStateMetadataService, IStateHealthCheckService, IStateAuthenticationService

## NuGet Packaging

`GeneratePackageOnBuild` is true. Packages output to `~/nuget-store/`. Debug builds get a `-dev` suffix. Connector repos consume this package via local NuGet source.

## Common Commands

```bash
dotnet build    # Build + generate NuGet package to ~/nuget-store/
dotnet test     # Run xUnit tests
```

## Development Workflow

After changing interfaces, build this repo first. Then rebuild the connector repo(s) that consume it. The portal API loads connector DLLs at runtime from `plugins-{state}/` directories.

## Related Repos

- **sebt-self-service-portal** -- main portal (API + Web + Infrastructure)
- **sebt-self-service-portal-dc-connector** -- DC plugin implementation
- **sebt-self-service-portal-co-connector** -- CO plugin implementation

## Branch Strategy

- `main` -- production
- `feature/*` -- in-progress work

## Gotchas
- **Build order matters.** After changing an interface, you must rebuild in order: this repo → connector repo(s) → restart portal API. Connector repos reference this package via `0.0.1-dev-*` wildcard, so they pick up the latest build from `~/nuget-store/` automatically — but only after a `dotnet restore`.
- **Adding a new interface?** Any connector that doesn't implement it will still load — MEF only resolves exports that exist. But the portal will get `null` from `GetExport<T>()` at runtime if the connector doesn't provide it.

## Security

- Never commit secrets, API keys, or PII.
- Use relative paths, not absolute paths containing email addresses.
