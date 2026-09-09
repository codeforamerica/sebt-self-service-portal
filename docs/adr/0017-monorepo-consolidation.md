# 17. Consolidate portal and state connectors into an `/apps` monorepo

Date: 2026-07-01

## Status

Accepted

## Context

SEBT was split across four repositories: the portal (`sebt-self-service-portal`) plus three
connector repos — `sebt-self-service-portal-state-connector` (the MEF plugin **contract**,
distributed as a NuGet package), `-co-connector`, and `-dc-connector` (per-state plugin
implementations). This fragmentation made multi-state changes span coordinated cross-repo PRs,
duplicated CI/CD configuration, and created version-drift between the contract package and its
consumers. It also made the inner development loop awkward (sibling-repo checkouts, a local
`~/nuget-store` NuGet feed, and post-build DLL copying into the portal).

DC-554 consolidates the portal and the **state** and **CO** connectors into a single monorepo,
while keeping the **DC** connector as an external repo (per ticket scope), with no regression to
the DC or CO deployment flows.

## Decision

- **Layout:** deployable app under `apps/portal`; connectors under `apps/connectors/{state,co}`;
  shared JS libraries stay at the top-level `packages/` (design-system, analytics). This follows the
  standard `apps/` (deployables) + `packages/` (shared libs) monorepo convention and keeps the
  existing `packages/` directory in place. Repo-wide config stays at the root: `.github/`,
  `pnpm-workspace.yaml`, `package.json`, `nuget.config`, `global.json`, `Directory.Build.props`,
  `SEBT.slnx`, `tofu/`.
- **History preserved:** the portal moved via `git mv`; the `state` and `co` connectors were imported
  with `git subtree add`, preserving their commit history.
- **Contract references:** portal projects reference the in-repo contract at
  `apps/connectors/state` via a `<ProjectReference>` when the project is present, and fall back to the
  NuGet package (staged in `nuget-store`) for the isolated API Docker build, which copies only the
  portal projects. The CO connector references the in-repo contract directly.
- **DC connector stays external:** the portal's own build/deploy workflows check out `dc-connector`
  and build it against the in-repo contract via
  `-p:StateConnectorInterfacesProject=<apps/connectors/state …>` (no version-fragile NuGet fallback).
  The DC connector's own CI/local-dev obtains the contract from a monorepo checkout — a follow-up PR
  in that repo (see Consequences).
- **Build ergonomics:** a top-level `SEBT.slnx` builds portal + in-repo connectors from the root;
  `global.json` pins the .NET SDK (10.0.200, roll-forward latestFeature); a scoped
  `apps/connectors/Directory.Build.props` imports the root defaults (NuGet security auditing, etc.)
  but relaxes `TreatWarningsAsErrors` for the imported connector code.
- **Cross-platform:** existing `$(OS)`-aware `HomeDir`/`nuget-store` handling and the `lsof`-on-Unix
  frontend-watch guard are preserved; new MSBuild paths use forward slashes; dev/CI scripts remain
  portable bash. The deeper `apps/portal/...` nesting raises Windows `MAX_PATH` risk — mitigated by a
  short `apps` prefix and documenting `git config core.longpaths true`.

Rejected alternative: **minimal-churn** (portal stays at repo root, connectors added under a
top-level `connectors/`). Lower churn, but the ticket called for the `/apps` shape and the team
preferred the conventional monorepo layout.

## Consequences

- Multi-state changes, CI/CD, and the inner loop are simplified; the contract has one canonical home.
- The old `-state-connector` and `-co-connector` repos should be **archived read-only** after import
  (history is preserved here). `-dc-connector` remains active and external.
- CI path filtering is implemented in `state-ci.yaml` and `playwright-e2e.yaml`: pull requests skip
  jobs whose inputs didn't change (portal-frontend vs backend/connectors vs enrollment checker),
  while pushes, manual dispatches, and any CI/build-infra change fail open and run everything.
- **Follow-ups:** (1) merge the companion `-dc-connector` PR that repoints its build defaults at the
  monorepo (contract path, plugin staging dir) — until then, `scripts/dev/build-dc.sh` and the
  workflows bridge the gap with explicit `-p:` overrides; (2) tune the CI path filters as real-world
  change patterns emerge; (3) optionally Central Package Management (`Directory.Packages.props`) to
  unify NuGet versions; (4) a `.github/CODEOWNERS` once the connector directories' owning teams are
  confirmed.
- Validation: the monorepo builds from the root (`dotnet build SEBT.slnx`); CI path-filtering and the
  dev-dc/dev-co non-prod deploys are validated in GitHub Actions + AWS before any prod release.
