# CLAUDE.md

Directory-scoped guidance for the Colorado connector. The root
[CLAUDE.md](../../../CLAUDE.md) covers repo-wide conventions, commands, and testing norms.

## Purpose

Colorado state plugin for the SEBT Self-Service Portal. Implements the plugin contract from
[`../state`](../state/CLAUDE.md) to connect with Colorado's CBMS (Colorado Benefits
Management System) API for case data, enrollment checks, and address updates. Loaded at
runtime by the portal via MEF.

See [README.md](./README.md) for setup and credential details.

## Technology

- .NET 10, C# with nullable reference types
- System.Composition (MEF) for plugin exports
- Microsoft.Kiota for CBMS API client generation
- libphonenumber-csharp, HybridCache
- xUnit for testing

## Structure

- **`src/SEBT.Portal.StatePlugins.CO`** — Main plugin. MEF exports, CBMS integration services.
- **`src/SEBT.Portal.StatePlugins.CO.CbmsApi`** — Kiota-generated CBMS API client + embedded mock test data (`TestData/CbmsMocks/`).
- **`src/SEBT.Portal.StatePlugins.CO.Tests`** — xUnit tests. User secrets for optional sandbox tests.
- `SEBT.Portal.StatePlugins.CO.slnx` — per-directory solution for IDE convenience; builds and CI use the root `SEBT.slnx`.

## MEF Exports

All services export `IStatePlugin` with `[ExportMetadata("StateCode", "CO")]`:

- ColoradoSummerEbtCaseService
- ColoradoEnrollmentCheckService
- ColoradoAddressUpdateService
- ColoradoAuthenticationService
- ColoradoStateMetadataService
- ColoradoHealthCheckService

## Plugin Build & Copy

The post-build target `CopyPlugins` copies DLLs to
`apps/portal/src/SEBT.Portal.Api/plugins-co/`. Override the destination with
`-p:PluginDestDir=<path>`, or disable staging entirely with `-p:CopyPlugins=false` (CI does
this where it stages plugin DLLs itself). Restart the portal API after building to pick up
changes.

## CBMS API Integration

- OAuth2 Client Credentials flow for authentication.
- Mock responses available for offline development — set `Cbms:UseMockResponses=true` in config.
- Mock data files are embedded resources in the CbmsApi project under `TestData/CbmsMocks/`.

## Configuration

CBMS credentials via user secrets or env vars (`Cbms__ClientId`, `Cbms__ClientSecret`). Set
`Cbms:UseMockResponses=true` for offline development without real credentials.

## Common Commands

```bash
dotnet build SEBT.slnx   # From the repo root: contract + portal + this plugin (+ DLL staging)
pnpm api:build-co        # From the repo root: build CO plugin and stage DLLs
dotnet test              # From this directory: run the CO test projects
```

## Dependencies

The plugin contract (`SEBT.Portal.StatesPlugins.Interfaces`) is referenced in-repo as a
ProjectReference from [`../state`](../state/). The NuGet fallback (from `~/nuget-store/`)
only applies to out-of-tree builds and should not be needed inside the monorepo.

## Gotchas

- **Never hand-edit `Generated/` files.** Everything under `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Generated/` is Kiota-generated from the CBMS OpenAPI spec and will be overwritten on regeneration.
- **Adding mock data files?** Files in `TestData/CbmsMocks/` must be `.json` or `.jsonc` — the csproj globs them as `EmbeddedResource` automatically. Other extensions won't be included.

## Security

- Never commit secrets or PII (including email addresses in file paths).
- CBMS credentials go in user secrets or CI secrets, never in code.
- Use relative paths, not absolute paths.
