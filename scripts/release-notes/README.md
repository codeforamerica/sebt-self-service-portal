# Release Notes Generator

Fetches merged PRs and produces a GitHub-style release notes document bucketed into
CO Specific, DC Specific, Portal Wide Changes, and Chores sections. Each entry includes
the PR author, a link to the PR, and a linked Jira ticket reference where present.
Output is written to `output/YYYY-MM-DD.md` (gitignored) so it can be reviewed before
sharing.

The same script runs in CI via the
[generate-release-notes workflow](../../.github/workflows/generate-release-notes.yml)
on both a weekly and a nightly cadence, and (for push-button releases) via
[resolve-live-sha.sh](./resolve-live-sha.sh) + a per-state `--since-sha` invocation.

## Prerequisites

The [`gh` CLI](https://cli.github.com) must be installed and authenticated:

```bash
gh auth login
```

## Usage

```bash
# From the repo root — date-window mode (nightly/weekly):
pnpm release-notes:generate                        # since the last release with this
                                                    # cadence's tag prefix (default: weekly-*);
                                                    # falls back to the last 7 days on that
                                                    # cadence's first-ever run
pnpm release-notes:generate --days=14              # override the first-run fallback window
pnpm release-notes:generate --tag-prefix=nightly   # look for nightly-* releases instead

# Exact commit-range mode (push-button releases) — see resolve-live-sha.sh for how
# --since-sha gets resolved from a state's live /api/build-info:
pnpm release-notes:generate --since-sha=<sha> --state-filter=dc
pnpm release-notes:generate --since-sha=<sha> --repo=codeforamerica/sebt-self-service-portal-dc-connector --git-dir=../sebt-self-service-portal-dc-connector
```

Output is written to `scripts/release-notes/output/YYYY-MM-DD.md`.
