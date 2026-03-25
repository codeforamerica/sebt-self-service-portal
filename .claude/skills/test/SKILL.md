---
name: test
description: Run unit tests across the SEBT multi-repo project in parallel — all tests, backend only, frontend only, or specific repos
allowed-tools: Bash(dotnet test:*), Bash(pnpm test:*)
---

# Multi-Repo Test Runner

Run tests across the SEBT portal's four repositories in parallel.

## Repositories

```
# Assumes all four SEBT repos are sibling directories under a common parent
SEBT_BASE="$(dirname "$(git rev-parse --show-toplevel)")"

portal="$SEBT_BASE/sebt-self-service-portal"
state_connector="$SEBT_BASE/sebt-self-service-portal-state-connector"
dc_connector="$SEBT_BASE/sebt-self-service-portal-dc-connector"
co_connector="$SEBT_BASE/sebt-self-service-portal-co-connector"
```

## Test Commands

| Repo | Backend command | Frontend command |
|------|----------------|------------------|
| **portal** | `dotnet test $portal/test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj` | `pnpm test --run` (cwd: `$portal/src/SEBT.Portal.Web`) |
| **state-connector** | `dotnet test $state_connector/src/SEBT.Portal.StatesPlugins.Interfaces.Tests/SEBT.Portal.StatesPlugins.Interfaces.Tests.csproj` | — |
| **dc-connector** | `dotnet test $dc_connector/test/SEBT.Portal.StatePlugins.DC.Tests/SEBT.Portal.StatePlugins.DC.Tests.csproj` | — |
| **co-connector** | `dotnet test $co_connector/src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj` | — |

## Invocation

Parse the ARGUMENTS string to determine scope. Arguments are combinable.

| Argument | Meaning |
|----------|---------|
| *(none)* | Run ALL backend + frontend tests across all repos |
| `backend` | Backend tests only, all repos |
| `frontend` | Frontend tests only (portal) |
| `portal` | Backend + frontend for portal only |
| `dc` | Backend tests for dc-connector only |
| `co` | Backend tests for co-connector only |
| `state-connector` | Backend tests for state-connector only |

**Combining arguments:** `/test backend dc` = backend tests for dc-connector only. `/test backend portal dc` = backend tests for portal and dc-connector.

When `frontend` is combined with a repo filter (e.g., `/test frontend dc`), ignore the repo filter for frontend since only portal has frontend tests — just run portal frontend tests.

## Execution Rules

- **Parallel execution:** Run ALL selected test suites as separate Bash tool calls in a single message.
- **Atomic commands:** Each test command is a separate Bash invocation. Never chain with `&&` or `;`.
- **Explicit paths:** Always use full paths based on the repository variables above.
- **Working directory for frontend:** Use the Bash `description` to clarify, and run: `cd $portal/src/SEBT.Portal.Web && pnpm test --run` — this is the ONE exception to the no-chaining rule since pnpm needs to run from the project directory.

## Reporting

### On all tests passing

Present a summary table:

| Repo | Suite | Result | Tests |
|------|-------|--------|-------|
| portal | backend | PASS | 42 passed |
| portal | frontend | PASS | 18 passed |
| dc-connector | backend | PASS | 15 passed |
| ... | ... | ... | ... |

Extract test counts from the command output:
- `dotnet test`: look for "Passed: X" or "Failed: X" in the summary line
- `pnpm test`: look for the Vitest summary line with test counts

### On any test failure

Show the summary table (with FAIL for failing suites), then include the **full output** for each failing suite below the table so the user can diagnose the issue.

## Rules

- **Always summarize:** Every invocation ends with the summary table.
- **Timeout:** Use a 5-minute (300000ms) timeout for each test command since integration tests with Testcontainers can be slow.
- **No builds:** This skill only runs tests. If a build failure prevents tests from running, report it and suggest the user build first.
