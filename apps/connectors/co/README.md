# Colorado (CO) connector

The Colorado implementation of the SEBT portal's state connector plugin. It integrates with
Colorado's CBMS (Colorado Benefits Management System) API for case data, enrollment checks,
and address updates, and is loaded by the portal API at runtime via MEF.

This directory was imported from the standalone `sebt-self-service-portal-co-connector`
repository (now archived) — see
[ADR 0017](../../../docs/adr/0017-monorepo-consolidation.md) for the consolidation rationale.

## Building and CI

Build from the repo root: `dotnet build SEBT.slnx` (or `pnpm api:build-co`, which also stages
the plugin DLLs into the portal's `plugins-co/` directory). The root `state-ci.yaml` workflow
builds and tests this connector alongside the portal; the plugin contract is referenced
in-repo from [`../state`](../state/README.md).

## CBMS credentials and mock responses

CI runs CBMS integration tests with mock responses by default, so no secrets are required
for the build to pass (sandbox health checks that require the real API are skipped).

For local development against the real CBMS sandbox, use .NET user secrets:

```bash
cd src/SEBT.Portal.StatePlugins.CO.Tests
dotnet user-secrets set "Cbms:ClientId" "<id>"
dotnet user-secrets set "Cbms:ClientSecret" "<secret>"
```

### Run integration tests with mock responses

To run the CBMS integration tests without real credentials or network access, enable mock
responses:

```bash
cd src/SEBT.Portal.StatePlugins.CO.Tests
dotnet user-secrets set "Cbms:UseMockResponses" "true"
```

Or use an environment variable: `Cbms__UseMockResponses=true`

### Local development with mock responses

The same mock responses used in integration tests are available when running the portal. Set
`Cbms:UseMockResponses=true` in the host configuration or `Cbms__UseMockResponses=true` as an
environment variable. The Colorado plugin will use mock CBMS API responses (you don't need
the client_id/secrets for this to work).

Mock response data lives in `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/` as
JSON files. Edit those files to change mock scenarios without recompiling.
