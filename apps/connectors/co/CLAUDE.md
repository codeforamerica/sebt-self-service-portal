# CLAUDE.md

## Project Purpose

Colorado state plugin for the SEBT Self-Service Portal. Implements plugin interfaces from the state-connector repo to connect with Colorado's CBMS (Colorado Benefits Management System) API for case data, enrollment checks, and address updates. Loaded at runtime by the portal via MEF.

See [README.md](./README.md) for full setup and credential details.

## Technology

- .NET 10, C# with nullable reference types
- System.Composition (MEF) for plugin exports
- Microsoft.Kiota for CBMS API client generation
- libphonenumber-csharp, HybridCache
- xUnit for testing

## Solution Structure

Solution file: `SEBT.Portal.StatePlugins.CO.slnx`

- **`src/SEBT.Portal.StatePlugins.CO`** — Main plugin. MEF exports, CBMS integration services.
- **`src/SEBT.Portal.StatePlugins.CO.CbmsApi`** — Kiota-generated CBMS API client + embedded mock test data (`TestData/CbmsMocks/`).
- **`src/SEBT.Portal.StatePlugins.CO.Tests`** — xUnit tests. User secrets for optional sandbox tests.

## MEF Exports

All services export `IStatePlugin` with `[ExportMetadata("StateCode", "CO")]`:

- ColoradoSummerEbtCaseService
- ColoradoEnrollmentCheckService
- ColoradoAddressUpdateService
- ColoradoAuthenticationService
- ColoradoStateMetadataService
- ColoradoHealthCheckService

## Plugin Build & Copy

Post-build target `CopyPlugins` copies DLLs to `../../../sebt-self-service-portal/src/SEBT.Portal.Api/plugins-co/`. Override with `PluginDestDir` env var. Restart the portal API after building to pick up changes.

## CBMS API Integration

- OAuth2 Client Credentials flow for authentication.
- Mock responses available for offline development — set `Cbms:UseMockResponses=true` in config.
- Mock data files are embedded resources in the CbmsApi project under `TestData/CbmsMocks/`.

## Configuration

CBMS credentials via user secrets or env vars (`Cbms__ClientId`, `Cbms__ClientSecret`). Set `Cbms:UseMockResponses=true` for offline development without real credentials.

## Common Commands

```bash
dotnet build    # Build + copy DLLs to portal plugins-co/
dotnet test     # Run tests (uses mock CBMS responses by default)
```

## Dependencies

Requires `SEBT.Portal.StatesPlugins.Interfaces` NuGet package from `~/nuget-store/`. If builds fail with package-not-found, build the state-connector repo first.

## Code Style

- C#: 4-space indent, Allman brace style, nullable reference types enabled
- See `.editorconfig` for full rules

## Related Repos

- **sebt-self-service-portal** — main portal (API + Web + Infrastructure)
- **sebt-self-service-portal-state-connector** — plugin interface contracts

## Branch Strategy

- `main` — production
- `feature/*` — in-progress work

## Gotchas
- **Never hand-edit `Generated/` files.** Everything under `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Generated/` is Kiota-generated from the CBMS OpenAPI spec and will be overwritten on regeneration.
- **Adding mock data files?** Files in `TestData/CbmsMocks/` must be `.json` or `.jsonc` — the csproj globs them as `EmbeddedResource` automatically. Other extensions won't be included.
- **NuGet package not found?** Build the state-connector repo first to populate `~/nuget-store/`, then `dotnet restore` here.

## Security

- Never commit secrets or PII (including email addresses in file paths).
- CBMS credentials go in user secrets or CI secrets, never in code.
- Use relative paths, not absolute paths.
