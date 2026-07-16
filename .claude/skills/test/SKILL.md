---
name: test
description: Run unit tests across the SEBT monorepo in parallel — all tests, backend only, frontend only, or specific suites (portal, connectors, dc)
allowed-tools: Bash(dotnet test:*), Bash(pnpm test:*)
---

# SEBT Test Runner

Run tests across the SEBT monorepo (portal + in-repo connectors) and the external
DC connector in parallel.

## Locations

```
# The portal, plugin contract, and CO connector live in this monorepo.
# Only the DC connector is a sibling checkout.
monorepo="$(git rev-parse --show-toplevel)"
dc_connector="$(dirname "$monorepo")/sebt-self-service-portal-dc-connector"
```

## Test Commands

| Suite | Command |
|-------|---------|
| **portal backend** | `dotnet test $monorepo/apps/portal/test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj` |
| **state contract** | `dotnet test $monorepo/apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces.Tests/SEBT.Portal.StatesPlugins.Interfaces.Tests.csproj` |
| **co connector** | `dotnet test $monorepo/apps/connectors/co/src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj` |
| **dc connector** | `dotnet test $dc_connector/test/SEBT.Portal.StatePlugins.DC.Tests/SEBT.Portal.StatePlugins.DC.Tests.csproj` |
| **portal frontend** | `cd $monorepo/apps/portal/src/SEBT.Portal.Web && pnpm test --run` |
| **enrollment checker** | `cd $monorepo/apps/portal/src/SEBT.EnrollmentChecker.Web && pnpm test run` |

`dotnet test SEBT.slnx` from the monorepo root covers the first three backend suites in one
command — prefer the per-project commands above when running suites in parallel or filtering.

## Invocation

Parse the ARGUMENTS string to determine scope. Arguments are combinable.

| Argument | Meaning |
|----------|---------|
| *(none)* | Run ALL backend + frontend suites |
| `backend` | All backend suites (portal, state contract, CO, DC) |
| `frontend` | Portal frontend + enrollment checker |
| `portal` | Portal backend + portal frontend |
| `dc` | DC connector backend only |
| `co` | CO connector backend only |
| `state-connector` | State contract backend only |

**Combining arguments:** `/test backend dc` = backend tests for the DC connector only.
`/test backend portal dc` = backend tests for portal and the DC connector.

When `frontend` is combined with a connector filter (e.g., `/test frontend dc`), ignore the
connector filter — only the portal apps have frontend tests.

## Execution Rules

- **Parallel execution:** Run ALL selected test suites as separate Bash tool calls in a single message.
- **Atomic commands:** Each test command is a separate Bash invocation. Never chain with `&&` or `;`.
- **Explicit paths:** Always use full paths based on the location variables above.
- **Working directory for frontend:** Use the Bash `description` to clarify, and run: `cd <app dir> && pnpm test --run` — this is the ONE exception to the no-chaining rule since pnpm needs to run from the project directory.

## Reporting

### On all tests passing

Present a summary table:

| Suite | Result | Tests |
|-------|--------|-------|
| portal backend | PASS | 1320 passed |
| portal frontend | PASS | 18 passed |
| dc connector | PASS | 15 passed |
| ... | ... | ... |

Extract test counts from the command output:
- `dotnet test`: look for "Passed: X" or "Failed: X" in the summary line
- `pnpm test`: look for the Vitest summary line with test counts

### On any test failure

Show the summary table (with FAIL for failing suites), then include the **full output** for each failing suite below the table so the user can diagnose the issue.

## Rules

- **Always summarize:** Every invocation ends with the summary table.
- **Timeout:** Use a 5-minute (300000ms) timeout for each test command since integration tests with Testcontainers can be slow.
- **No builds:** This skill only runs tests. If a build failure prevents tests from running, report it and suggest the user build first.
