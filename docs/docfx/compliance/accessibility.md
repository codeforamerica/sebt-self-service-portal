---
description: The accessibility target, the rules that apply while building, what is checked automatically, and what is not covered.
keywords: accessibility a11y WCAG 2.1 AA pa11y axe USWDS contrast keyboard screen reader focus ARIA Section 508
---

# Accessibility

Both web apps target **WCAG 2.1 AA**. This page states the rules that apply while building, what the automated
checks actually cover, and what they do not. The last part matters most: the checks are narrower than their presence
suggests, and reading them as full coverage would be a mistake.

## Rules that apply while building

These are working rules for anyone writing components, not aspirations.

- **Use USWDS primitives first.** USWDS components meet the baseline at the primitive level. Reaching for an existing
  primitive is the cheapest way to stay compliant, and the shared components in `@sebt/design-system` already
  encapsulate the ARIA wiring and class composition.
- **Supply ARIA labels and roles when composing something new.** A primitive carries its own semantics; a composition
  built from raw markup does not inherit them.
- **Keep focus states visible.** Every interactive element must be reachable by keyboard and must show where focus
  is. Removing an outline without replacing it is a defect.
- **Take colors from design tokens rather than hardcoding them.** The tokens are contrast-tested. A literal hex value
  bypasses that, cannot be rethemed per state, and is how contrast regressions get in. This applies to the
  documentation site too, where `template/sebt/public/main.css` maps the tokens onto the template's variables.

## What runs automatically

| Check | Scope | Standard | Runs in CI |
| --- | --- | --- | --- |
| `pa11y-ci`, portal | 2 URLs: `/` (which lands on `/login`) and `/login` | WCAG2AA, `axe` and `htmlcs` runners | Yes, the `a11y` job in `playwright-e2e.yaml` |
| `pa11y-ci`, enrollment checker | 4 URLs: `/`, `/disclaimer`, `/check`, `/closed` | WCAG2AA, `axe` runner | **No** |
| `axe-core` in the browser | Whatever the developer is looking at | `color-contrast`, `landmark-one-main`, `region` | No, development only |

The portal's `a11y` job runs the full stack, a real API and web server, and fires when either the backend or the
frontend changed. Configuration is in each app's `.pa11yci.json`.

The in-browser check is wired through `AxeProvider` and `src/lib/axe.ts`. It reports to the console in development
and is compiled out of production builds, so it catches problems while someone is working rather than gating a merge.

The enrollment checker's config exists and works, but `pnpm ci:test:a11y` only enters the portal workspace, so
nothing runs it automatically. Until that is wired up, run it by hand:

```bash
pnpm --filter @sebt/enrollment-checker test:a11y
```

## What is not covered

- **Every authenticated page.** The portal scan stops at the login screen. The dashboard, the household and card
  flows, the address form, and the whole ID-proofing path are never scanned. That is the majority of the product and
  the part with the most complex interactions.
- **The enrollment checker, in CI.** See above.
- **Keyboard and assistive-technology behaviour.** There are no keyboard-specific end-to-end tests and no axe
  integration in the Playwright suite. The suite does select elements by role roughly 81 times, which means a missing
  or wrong accessible name tends to break a test, but that is a side effect of how the tests are written rather than
  accessibility coverage. Tab order, focus management across route changes, focus trapping in modals, and live-region
  announcements are not tested.
- **Manual audit.** No screen-reader pass, no audit by a disabled user, and no VPAT or Section 508 conformance
  statement is recorded in this repository.

One configuration detail worth knowing: the portal's `.pa11yci.json` sets `hideElements: ".axe-exclude"`, which
removes any element carrying that class from the scan. Nothing in the portal uses the class today, so it changes
nothing now, but it is a quiet way to make a violation disappear rather than fix it.

## Gaps

| Gap | Effect |
| --- | --- |
| Authenticated pages are unscanned | Most of the product has no automated accessibility check |
| The enrollment checker is not in CI | A regression there reaches a release unnoticed |
| No keyboard or screen-reader testing | Failures that a static scan cannot see go undetected |
| No manual audit or conformance statement | Nothing to hand a partner agency that asks for one |

An automated scan of two pre-login pages is a floor, not a conformance claim. Treating WCAG 2.1 AA as met because
the `a11y` job is green would misstate where this project actually is.
