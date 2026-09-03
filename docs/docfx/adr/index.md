# Architecture Decisions

This project records architecturally significant decisions as ADRs, in the format
[described by Michael Nygard](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions): the
decision, the context that forced it, and the consequences the team accepted.

Every record in `docs/adr/` is published here verbatim. Use the navigation to browse them. The list is generated from
the directory, so a new ADR appears on the next docs build.

## Adding a decision

New ADRs go in `docs/adr/`. All 30 records follow the same header, and new ones should too:

```markdown
# N. Title in sentence case

Date: 2026-09-02

## Status

Accepted

## Context

## Decision

## Consequences
```

The site's navigation label comes from the H1, so that line is the one that has to be right. The `Date:` line and
`## Status` section are what make the set sortable and summarizable. They were normalized
across the older records, so keep them.
