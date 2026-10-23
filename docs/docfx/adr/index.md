---
description: Every architecture decision record (ADR) for the Summer EBT Portal, with its standing, date, and author.
keywords: ADR architecture decision records decisions log accepted proposed superseded rationale context consequences
---

# Architecture Decision Records

[!INCLUDE [Decision records](_cards.md)]

## Adding a decision record

New ADR markdown files should be added to `/docs/adr/`, in the format
[described by Michael Nygard](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions):

```markdown
# N. Title in sentence case

Date: 2026-09-14

## Status

Accepted

## Context

## Decision

## Consequences
```

When ADRs are added to `/docs/adr/` they will appear on this page:

- The **H1** provides each card's number and title, and the site's navigation label
- The **`Date:` line** is the decision date
- The **`## Status`** provides the label:  `Proposed`, `Accepted`, `Superseded`, `Deprecated`, or `Rejected`. An unrecognized value fails the build.
- The author on each card comes from git
