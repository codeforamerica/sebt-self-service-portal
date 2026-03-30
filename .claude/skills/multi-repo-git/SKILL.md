---
name: multi-repo-git
description: Use when performing git operations (sync, branch, status) across the SEBT multi-repo project — switching branches, pulling latest, creating feature branches, or checking repo state
allowed-tools: Bash(git -C:*)
---

# Multi-Repo Git Operations

Manage git operations across the SEBT portal's four repositories in parallel.

## Repositories

```
# Assumes all four SEBT repos are sibling directories under a common parent
SEBT_BASE="$(dirname "$(git rev-parse --show-toplevel)")"

portal="$SEBT_BASE/sebt-self-service-portal"
state_connector="$SEBT_BASE/sebt-self-service-portal-state-connector"
dc_connector="$SEBT_BASE/sebt-self-service-portal-dc-connector"
co_connector="$SEBT_BASE/sebt-self-service-portal-co-connector"
```

All git commands MUST use `git -C <repo-path>` to be explicit about the target directory.

## Operations

### sync — Checkout main and pull latest

Default: all four repos. User may specify a subset.

1. Run `git -C <path> checkout main` on each repo (parallel)
2. Run `git -C <path> pull` on each repo (parallel)
3. Report results in a summary table

### branch — Create or checkout a feature branch

Requires: branch name from user, and which repos to target (ask if not specified).

- To create: `git -C <path> checkout -b <branch>`
- To checkout existing: `git -C <path> checkout <branch>`

Run in parallel across targeted repos. Report results.

### status — Show state of all repos

Run `git -C <path> status --short --branch` on all four repos in parallel.

Present as a summary table:

| Repo | Branch | Clean? | Tracking |
|------|--------|--------|----------|

## Rules

- **Parallel execution:** Always run independent git commands across repos in parallel (multiple Bash tool calls in one message).
- **Atomic commands:** Each `git -C` call is a separate Bash invocation. Never chain with `&&` or `;`.
- **Read-only by default:** Only `sync` and `status` run without confirmation. For `branch`, confirm the branch name and target repos before executing.
- **Always summarize:** End every operation with a concise table showing results per repo.
