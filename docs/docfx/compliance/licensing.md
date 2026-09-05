---
description: The license this project is released under, the dependency license policy, and how the policy is checked.
keywords: license licensing legal copyright PolyForm noncommercial dependencies attribution audit compliance SPDX
---

# Licensing

This page records the license this project is released under, the policy that governs the licenses of its
dependencies, and how much of that policy is checked automatically. It is a summary. The license text itself
governs, and questions about a specific use belong with counsel rather than with this page.

## The project license

The project is released under the **PolyForm Noncommercial License 1.0.0**.

- Full text: [LICENSE.md](https://github.com/codeforamerica/sebt-self-service-portal/blob/main/LICENSE.md)
- Canonical text: <https://polyformproject.org/licenses/noncommercial/1.0.0>

The license grants rights to use, distribute, and modify the software for permitted purposes, and it defines a
permitted purpose as a noncommercial one. It is a source-available license, not an OSI-approved open source
license, so tooling that assumes an open source license can classify it incorrectly. Use by a government agency to
administer benefits is the intended case. Any commercial use needs a separate grant from the licensor.

### Where the license applies

| Location | License file |
| --- | --- |
| Repository root | `LICENSE.md` |
| `apps/connectors/state/` | Identical copy of the root file |
| `apps/connectors/co/` | Identical copy of the root file |
| The DC connector repository | None present. See [Known gaps](#known-gaps). |

The two in-repo connector copies are byte-identical to the root file.

## Dependency licenses

### The policy

Two files under `.github/workflows/scripts/licenses/` hold the policy:

| File | Contents |
| --- | --- |
| `allowed-licenses.json` | `MIT`, `Apache-2.0`, `BSD-2-Clause`, `BSD-3-Clause`, `MS-PL` |
| `license-mappings.json` | Maps one legacy ASP.NET Core license URL to `Apache-2.0`, because that package declares a URL rather than an SPDX identifier |

### The audit

`.github/workflows/scripts/license-audit.sh` runs the `nuget-license` dotnet tool against `SEBT.slnx` and writes
`output/backend-dependencies.csv`. Each row carries the package, its version, its license, and an `errors` column
holding any validation error for that package.

`state-ci.yaml` runs the script in the `Generate Backend Dependencies CSV` step, on backend changes, on the
non-Docker path.

Because the input is `SEBT.slnx`, the audit covers the portal projects, the connector contract, and the Colorado
connector together.

### What the audit does not cover

| Not covered | Detail |
| --- | --- |
| Every npm dependency | The audit reads NuGet packages only. No equivalent runs for the frontend. |
| Build failure on a violation | The script sets `-e` and `-u` but not `pipefail`. The tool's output is piped to `jq`, so the step reports `jq`'s exit status. A violation is recorded in the CSV rather than stopping the build. |

### Frontend dependency licenses

The frontend has no automated policy check, so these figures come from running `pnpm licenses list` against the
workspace. They describe the tree at the time of writing and will drift.

858 packages resolve to 20 distinct license expressions. 793 of them fall under the identifiers the NuGet policy
allows, and 65 do not.

| License | Packages | On the NuGet allow-list |
| --- | --- | --- |
| MIT | 654 | Yes |
| Apache-2.0 | 89 | Yes |
| ISC | 40 | No |
| BSD-3-Clause | 30 | Yes |
| BSD-2-Clause | 20 | Yes |
| CC0-1.0 | 5 | No |
| MPL-2.0 | 4 | No |
| MIT-0, BlueOak-1.0.0, LGPL-3.0-only | 2 each | No |
| 9 further expressions | 1 each | Mixed |

Two entries deserve a note:

- **LGPL-3.0-only** covers `pa11y` and `pa11y-ci`. Both are `devDependencies` of the two web apps, used by the
  `test:a11y` script. Neither ships to a browser or a server.
- **LGPL-3.0-or-later** covers a platform-specific `libvips` binary that `sharp` pulls in. `sharp` reaches the tree
  through Next.js image handling.

USWDS, the design system the apps are built on, is offered under `Apache 2.0 OR CC0 1.0 OR MIT`.

To reproduce the figures:

```bash
pnpm licenses list
```

## Known gaps

These are recorded so that a maintainer can act on them. None is resolved.

1. The DC connector repository holds no `LICENSE.md`, while the two in-repo connectors do. A reader of that
   repository has nothing stating the terms.
2. No npm license audit exists, so 858 frontend packages are governed by no automated policy. The allow-list also
   omits `ISC`, which 40 of them use.
3. The backend audit records violations without failing the build, for the `pipefail` reason above.
4. No `package.json` in the workspace declares a `license` field. Tooling that reads that field finds nothing.

## Related

- [Architecture Decisions](../adr/index.md) record the choices behind the dependencies listed here.
- [.NET API Reference](../api/index.md) covers the C# projects the backend audit reads.
