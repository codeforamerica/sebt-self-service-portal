# DC connector — maintained externally

Unlike `state/` (the plugin contract) and `co/` (the Colorado implementation), the
**DC connector is not imported into this monorepo.** Its source lives in a separate
repository and is checked out on demand by CI:

- **Repository:** [codeforamerica/sebt-self-service-portal-dc-connector](https://github.com/codeforamerica/sebt-self-service-portal-dc-connector)

This directory is a placeholder so the layout is self-documenting — anyone browsing
`apps/connectors/` can see that DC exists but is sourced out-of-tree.

## How it consumes the contract

The DC connector builds against the same plugin contract as CO
(`SEBT.Portal.StatesPlugins.Interfaces`, in `apps/connectors/state/`). It resolves
that contract the same way the in-repo projects do — a dual reference strategy:

1. **Source (CI):** the monorepo workflows check out the DC connector, then build its
   plugin against the in-repo contract by passing
   `-p:StateConnectorInterfacesProject="$GITHUB_WORKSPACE/apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj"`.
2. **NuGet fallback:** when the contract source is absent (out-of-tree builds), it
   restores the `SEBT.Portal.StatesPlugins.Interfaces` package from the local
   `~/nuget-store` feed. The contract keeps `GeneratePackageOnBuild` so this feed
   stays populated.

The compiled DC plugin DLLs are staged into
`apps/portal/src/SEBT.Portal.Api/plugins-dc/` (gitignored) and loaded at runtime via
MEF when `STATE=dc`.

## Where it's built

- `.github/workflows/deploy-ecr.yaml` — DC dev (Docker/ECR)
- `.github/workflows/release-iis-dc.yaml` — DC prod (IIS bundle)
- `.github/workflows/build-and-seed-dc-source.yaml` — DC seed/source build

See [docs/adr/0017-monorepo-consolidation.md](../../../docs/adr/0017-monorepo-consolidation.md)
for why DC stays external while `state/` and `co/` were consolidated.
