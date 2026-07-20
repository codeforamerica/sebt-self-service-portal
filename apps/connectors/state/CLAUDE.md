# CLAUDE.md

Directory-scoped guidance for the plugin contract. The root [CLAUDE.md](../../../CLAUDE.md)
covers repo-wide conventions, commands, and testing norms.

## Purpose

Plugin interface contracts (`SEBT.Portal.StatesPlugins.Interfaces`) for the SEBT Self-Service
Portal's multi-state plugin system. The portal and the in-repo CO connector reference this
project directly; the external DC connector consumes it as a NuGet package from
`~/nuget-store/` or via `-p:StateConnectorInterfacesProject=<path>`.

## Structure

- `src/SEBT.Portal.StatesPlugins.Interfaces` — interface contracts + shared models
- `src/SEBT.Portal.StatesPlugins.Interfaces.Tests` — xUnit tests
- `SEBT.Portal.StateConnector.slnx` — per-directory solution for IDE convenience; builds and
  CI use the root `SEBT.slnx`

### Key Interfaces

IStatePlugin (base marker), ISummerEbtCaseService, IEnrollmentCheckService, IAddressUpdateService, IStateMetadataService, IStateHealthCheckService, IStateAuthenticationService

## NuGet Packaging

`GeneratePackageOnBuild` is true: every build packs to `~/nuget-store/`, and Debug builds get
a `-dev` suffix. The package is a fallback only — in-repo consumers use a ProjectReference.
The version is defined once as `StateConnectorInterfacesVersion` in the root
`Directory.Build.props`.

## Development Workflow

After changing an interface, `dotnet build SEBT.slnx` from the repo root rebuilds the
contract, the portal, and the CO connector together — then restart the portal API. Only the
external DC connector needs a separate build (`pnpm api:build-dc`).

## Gotchas

- **Adding a new interface?** Any connector that doesn't implement it will still load — MEF only resolves exports that exist. But the portal will get `null` from `GetExport<T>()` at runtime if the connector doesn't provide it.
- **Bumping the contract version?** Update `StateConnectorInterfacesVersion` in the root `Directory.Build.props`, and keep the external dc-connector's pinned copy in step.

## Security

- Never commit secrets, API keys, or PII.
- Use relative paths, not absolute paths containing email addresses.
