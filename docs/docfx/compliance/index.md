---
description: The obligations this project carries, what is checked automatically, and where each rule is written down.
keywords: compliance accessibility WCAG a11y security CodeQL Dependabot licensing privacy PII identity proofing IAL audit governance
---

# Compliance

This project carries obligations in four areas. This page says what each one covers, what runs automatically, and
where the rule is written down. Only licensing has a full page so far.

| Area | Documented in | Checked by |
| --- | --- | --- |
| Licensing | [Licensing](licensing.md) | `license-audit.sh` in `state-ci.yaml`, NuGet packages only |
| Accessibility | `CLAUDE.md`, target WCAG 2.1 AA | `pnpm ci:test:a11y` in `playwright-e2e.yaml`, configured by `.pa11yci.json` |
| Application security | `CLAUDE.md`, referencing the OWASP Top Ten | CodeQL and Dependabot, below |
| Identity proofing | [ADR 0027](../adr/0027-unified-id-proofing-requirements.md) and `docs/config/ial/README.md` | Server-side checks at the data boundary |

## What runs automatically

### CodeQL

`.github/workflows/codeql.yml` scans four languages: `actions`, `csharp`, `javascript-typescript`, and `python`. It
runs on every push and pull request against `main`, and again on a weekly schedule.

### Dependabot

`.github/dependabot.yml` covers six package ecosystems: npm, NuGet, GitHub Actions, pip in two locations, and
Terraform.

### Accessibility

The two web apps target WCAG 2.1 AA. `pa11y-ci` runs against the portal from `playwright-e2e.yaml`, and the
Playwright suite covers keyboard and screen-reader behaviour that a static scan cannot reach.

Accessibility is a design constraint as much as a test. `CLAUDE.md` records the rules that apply while writing
components: use USWDS primitives first, supply ARIA labels and roles when composing new ones, keep focus states
visible, and take colors from design tokens rather than hardcoding them, so contrast stays tested.

### Licensing

See [Licensing](licensing.md) for the license itself, the dependency policy, and the four gaps recorded there.

## Rules that live in the codebase rather than here

Several obligations are enforced in code and recorded in `CLAUDE.md` rather than on this site:

- **PII at rest.** Identifiers used only for lookups are hashed with `IIdentifierHasher`, never stored in cleartext.
- **Data boundary.** Authorization is enforced at the API endpoint that returns the data, not in the UI. A user
  without sufficient identity assurance gets a 403 with structured problem details, not a filtered 200.
- **Content Security Policy.** A strict policy is set in the portal's `proxy.ts`. Any new browser-side call to an
  external domain needs its own directive entry, and the policy is not enforced in local development or in tests,
  so a missing entry surfaces only in a deployed environment.
- **Secrets.** Loaded from environment variables or Docker secret files, never committed.

## Gaps

- Accessibility, security, and identity proofing have no page here. The rules exist, but they are spread across
  `CLAUDE.md`, ADRs, and workflow files.
- The licensing gaps are listed on the [Licensing](licensing.md) page and are not repeated here.
