---
description: Every architecture decision recorded for the Summer EBT Self-Service Portal, with its standing, date, and author.
keywords: ADR architecture decision records decisions log accepted proposed superseded rationale context consequences
---

# Architecture Decisions

Why the portal is built the way it is. Each record states one decision, the context that forced it, and the
consequences the team accepted.

[!INCLUDE [Decision records](_cards.md)]

## Adding a decision

New ADRs go in `docs/adr/`, in the format
[described by Michael Nygard](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions). The
list above is built from that directory on every docs build, so a new record appears without editing this page.
Number a new record with the next unused number: they are unique and sequential, and reusing one is what forced
nine records to be renumbered in the past.

```markdown
# N. Title in sentence case

Date: 2026-09-14

## Status

Accepted

## Context

## Decision

## Consequences
```

Three parts of that header are load-bearing, because the cards are generated from them:

- The **H1** gives each card its number and title, and the site's navigation label.
- The **`Date:` line** is the decision date the card shows. It is not derived from git, because a decision can
  predate the file, and two of these records do.
- The **`## Status`** value sets which group the card falls into. Use `Proposed`, `Accepted`, `Superseded`,
  `Deprecated`, or `Rejected`. An unrecognized value fails the build rather than publishing a blank badge.

The author on each card comes from git, so there is nothing to write down for it.
