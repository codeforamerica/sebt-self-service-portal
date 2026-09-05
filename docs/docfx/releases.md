---
description: Where to find release notes, what the release tags mean, and how to see what changed between two releases.
keywords: release releases changelog notes tags versions deploy shipped history diff compare nightly weekly
---

# Releases

Release notes live on GitHub, not in this site. This page says where to find them and how to read the tags.

| What you want                     | Where to go                                                                                  |
| --------------------------------- | -------------------------------------------------------------------------------------------- |
| Every published release           | [Releases](https://github.com/codeforamerica/sebt-self-service-portal/releases)              |
| The newest one                    | [Latest release](https://github.com/codeforamerica/sebt-self-service-portal/releases/latest) |
| What changed between two releases | [Compare tags](https://github.com/codeforamerica/sebt-self-service-portal/compare)           |

## The tags

| Tag pattern           | Example               | Meaning                                                              |
| --------------------- | --------------------- | -------------------------------------------------------------------- |
| `nightly-YYYY-MM-DD`  | `nightly-2026-09-03`  | Automatic, every day at midnight UTC. Published, not a draft.        |
| `weekly-YYYY-MM-DD`   | `weekly-2026-09-01`   | Automatic, every Monday at 09:00 UTC. Created as a draft for review. |
| `YYYY.MM.DD-dc`       | `2026.08.31-dc`       | A District of Columbia release.                                      |
| `YYYY.MM.DD-colorado` | `2026.08.31-colorado` | A Colorado release.                                                  |

Both state tag suffixes have older spellings in the tag list, including `-co` and a bare date. Read the tag list
rather than assuming a pattern holds for older entries.

## How the notes are produced

`.github/workflows/generate-release-notes.yml` runs `scripts/release-notes/generate.ts`. It reads merged pull
requests and groups them into CO Specific, DC Specific, Portal Wide Changes, and Chores. Each entry carries the
author, a link to the pull request, and a Jira reference where one exists.

One workflow serves both cadences. It reads the cron expression that fired the run to decide whether the release is
weekly or nightly. A manual run is treated as nightly and always stays a draft, on the assumption that someone
triggering it by hand is testing.

Push-button releases take a commit range instead of a date window. `scripts/release-notes/resolve-live-sha.sh`
reads the currently deployed commit from a state's live `/api/build-info`, and the generator is invoked with
`--since-sha` so the notes cover exactly what is not yet deployed.

The generator writes to `scripts/release-notes/output/`, which is git-ignored. The notes reach people through the
GitHub release body rather than through a file in the repository. `scripts/release-notes/README.md` documents
running it locally, which needs an authenticated `gh` CLI.

## Per-page history

Every page under Docs carries **View changelog** below its title, pointing at that file's commit history. That is
the reliable way to see how one page changed and why, since the documentation is not versioned alongside releases.
