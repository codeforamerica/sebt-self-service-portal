# State connector contract

The MEF plugin contract (`SEBT.Portal.StatesPlugins.Interfaces`) for the SEBT Self-Service
Portal's multi-state plugin system. Every state connector implements these interfaces; the
portal API loads the implementations at runtime from `plugins-{state}/` directories.

This directory was imported from the standalone `sebt-self-service-portal-state-connector`
repository (now archived) — see
[ADR 0017](../../../docs/adr/0017-monorepo-consolidation.md) for the consolidation rationale.

## Building

Build from the repo root — the portal and the CO connector reference this project directly:

```bash
dotnet build SEBT.slnx
```

Every build also packs the contract to `~/nuget-store/` for consumers that build out-of-tree:
the isolated API Docker image build and the external
[DC connector](../dc/README.md). The package version is defined once as
`StateConnectorInterfacesVersion` in the root `Directory.Build.props`.

See the root [README](../../../README.md) and [CLAUDE.md](../../../CLAUDE.md) for repo-wide
setup, commands, and conventions.
