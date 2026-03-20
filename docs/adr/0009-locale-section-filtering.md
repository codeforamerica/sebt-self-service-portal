# 9. Section-level filtering for locale generation

Date: 2026-03-16

## Status

Accepted

## Context

The `generate-locales.js` script maps CSV sections to i18n namespaces. Multiple CSV sections can map to the same namespace — for example, both S1 (enrollment checker's "Personal Information" page) and S3 (portal's profile management) map to the `personalInfo` namespace via the `sectionToNamespace` config.

The script already has an `--app` CLI flag that filters at the **namespace** level, but `personalInfo` and `confirmInfo` are mapped as `'all'` (shared between portal and enrollment), so the `--app` filter lets both S1 and S3 rows through for these namespaces.

Because S3 rows appear later in the CSV and the collision resolution uses "last non-empty value wins" logic, the portal's values overwrite the enrollment checker's. This caused the enrollment checker to display headings like "Are you sure you want to delete this address?" instead of "Check if your child is already enrolled in Summer EBT."

## Decision

Add a `--sections` CLI flag to `generate-locales.js` that filters at the **CSV row** level, complementing the existing `--app` namespace-level filter. When `--sections` is provided, only rows from the specified CSV sections are processed.

- `--app` controls **which namespaces** appear in the output
- `--sections` controls **which CSV rows** contribute to those namespaces

The enrollment checker's `copy:generate` script uses `--sections S1,GLOBAL` to exclude portal-specific sections (S3, S4, etc.) while keeping shared content.

## Alternatives Considered

1. **Separate CSV files per app** — creates content management overhead. State content teams maintain a single CSV per state; splitting would double maintenance.
2. **Rename S1 namespaces** (e.g., `enrollmentPersonalInfo`) — breaks the predictable naming convention and requires changing all component `useTranslation()` calls.
3. **Change collision resolution to warn/error** — doesn't solve the problem, just surfaces it. Both S1 and S3 legitimately need the `personalInfo` namespace for their respective apps.

## Consequences

- Each app's `copy:generate` script specifies which sections it consumes.
- Content authors continue using the shared CSV.
- New apps must declare their section filter.
- When `--sections` is not provided, all sections are processed (backward compatible).
