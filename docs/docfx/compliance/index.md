---
description: The obligations this project carries, what is checked automatically, and where each rule is written down.
keywords: compliance accessibility WCAG a11y security CodeQL Dependabot licensing privacy PII identity proofing IAL audit governance
---

# Compliance

This project carries obligations in four areas. This page says what each one covers, what runs automatically, and
where the rule is written down. Security and licensing have full pages; accessibility does not yet.

| Area | Documented in | Checked by |
| --- | --- | --- |
| Application security | [Security and privacy](security.md) | CodeQL and Dependabot, below |
| Licensing | [Licensing](licensing.md) | `license-audit.sh` in `state-ci.yaml`, NuGet packages only |
| Accessibility | `CLAUDE.md`, target WCAG 2.1 AA | `pnpm ci:test:a11y` in `playwright-e2e.yaml`, configured by `.pa11yci.json` |
| Identity proofing | [ADR 0027](../adr/0027-unified-id-proofing-requirements.md) and `docs/config/ial/README.md`, summarised in [Security and privacy](security.md) | Server-side checks at the data boundary |

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

Several obligations are enforced in code rather than by a document: PII hashing and encryption at rest,
authorization at the data boundary, the Content Security Policy, secrets handling, and credential rotation.
[Security and privacy](security.md) describes each one and names where it is implemented, so it is not repeated
here. The working rules themselves are in `CLAUDE.md`.

## Gaps

- Accessibility has no page here. The target and the automated checks exist, but the rules are spread across
  `CLAUDE.md` and workflow files.
- The security gaps, including the absence of a threat model, an SBOM, and secret scanning in CI, are listed on the
  [Security and privacy](security.md) page and are not repeated here.
- The licensing gaps are listed on the [Licensing](licensing.md) page and are not repeated here.
