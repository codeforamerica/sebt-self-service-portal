# Release Notes Generator

Fetches merged PRs from the past week and produces a plain-language markdown summary
bucketed by state (Colorado, DC, or both). Output is written to `output/YYYY-MM-DD.md`
(gitignored) so it can be reviewed before sharing.

The same script runs in CI via the [Weekly Release Notes workflow](../../.github/workflows/weekly-release-notes.yml).

## Prerequisites

The [`gh` CLI](https://cli.github.com) must be installed and authenticated:

```bash
gh auth login
```

## Usage

```bash
# From the repo root:
pnpm release-notes:generate           # last 7 days (default)
pnpm release-notes:generate --days=14 # custom lookback window
```

Output is written to `scripts/release-notes/output/YYYY-MM-DD.md`.
