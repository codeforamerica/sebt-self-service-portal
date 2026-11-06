---
description: How we approach accessibility, the tooling in this repository that supports it, what that tooling does and doesn’t cover, and where to find authoritative guidance.
keywords: accessibility a11y WCAG 2.1 AA ADA Title II pa11y axe USWDS design tokens contrast keyboard focus screen reader ARIA Assistiv
---

# Accessibility

This guide covers how we approach accessibility, the tooling in this repository that supports it, what that tooling does and doesn’t cover, and where to find authoritative guidance.

## The standard we build to

Both apps target [**WCAG 2.1 Level AA**](https://www.w3.org/TR/WCAG21/). Where it matters for people filling out forms on phones, some design choices go beyond that. For example, interactive targets are at least 44px, which is the AAA Target Size criterion.

WCAG 2.1 AA is also the legal benchmark for state partners. Under Title II of the Americans with Disabilities Act, [state and local government web content and mobile apps must meet WCAG 2.1 Level AA](https://www.ecfr.gov/current/title-28/chapter-I/part-35/subpart-H/section-35.200). State and local governments with a population of 50,000 or more must comply by **April 26, 2027**, and smaller governments and special district governments by April 26, 2028. See the Department of Justice’s [fact sheet on the Title II web and mobile accessibility rule](https://www.ada.gov/resources/2024-03-08-web-rule/) for a plain-language summary.

## Our approach

### Accessibility is a shared responsibility

Accessibility is built in from the start and shared between design and engineering, not saved for the end of the project. Achieving and testing for WCAG 2.1 compliance is a standing part of UX and engineering collaboration on the team. New designers are onboarded by reviewing WCAG 2.1 AA expectations and USWDS constraints, and by identifying screens to audit early.

### Design principles for form flows

The Enrollment Checker and Portal follow Code for America’s client form accessibility guidelines, which map directly to WCAG success criteria:

| Principle | What it means in practice | Key WCAG criteria |
| --- | --- | --- |
| **Works on any device** | Usable on phones in either orientation, when zoomed in, and without scrolling in two directions. | [1.3.4 Orientation](https://www.w3.org/TR/WCAG21/#orientation), [1.4.4 Resize Text](https://www.w3.org/TR/WCAG21/#resize-text), [1.4.10 Reflow](https://www.w3.org/TR/WCAG21/#reflow) |
| **Keyboard operable** | Everything works with Tab, Shift+Tab, Space, Enter, and the arrow keys, and every focusable element has a visible focus state. | [2.1.1 Keyboard](https://www.w3.org/TR/WCAG21/#keyboard), [2.1.2 No Keyboard Trap](https://www.w3.org/TR/WCAG21/#no-keyboard-trap), [2.4.3 Focus Order](https://www.w3.org/TR/WCAG21/#focus-order), [2.4.7 Focus Visible](https://www.w3.org/TR/WCAG21/#focus-visible) |
| **Readable and high contrast** | Large sans-serif type and dark text on light backgrounds, without relying on color alone. | [1.4.1 Use of Color](https://www.w3.org/TR/WCAG21/#use-of-color), [1.4.3 Contrast (Minimum)](https://www.w3.org/TR/WCAG21/#contrast-minimum), [1.4.11 Non-text Contrast](https://www.w3.org/TR/WCAG21/#non-text-contrast), [3.2.4 Consistent Identification](https://www.w3.org/TR/WCAG21/#consistent-identification) |
| **Text alternatives** | Images and icons have text alternatives, and icon buttons and links have meaningful labels. | [1.1.1 Non-text Content](https://www.w3.org/TR/WCAG21/#non-text-content), [1.3.5 Identify Input Purpose](https://www.w3.org/TR/WCAG21/#identify-input-purpose), [2.4.4 Link Purpose (In Context)](https://www.w3.org/TR/WCAG21/#link-purpose-in-context), [2.5.3 Label in Name](https://www.w3.org/TR/WCAG21/#label-in-name) |
| **Clear page structure** | One main question per page as the heading, about 1–5 sub-questions, a logical reading order, meaningful regions, and the page language set. | [1.3.1 Info and Relationships](https://www.w3.org/TR/WCAG21/#info-and-relationships), [1.3.2 Meaningful Sequence](https://www.w3.org/TR/WCAG21/#meaningful-sequence), [2.4.2 Page Titled](https://www.w3.org/TR/WCAG21/#page-titled), [2.4.6 Headings and Labels](https://www.w3.org/TR/WCAG21/#headings-and-labels), [3.1.1 Language of Page](https://www.w3.org/TR/WCAG21/#language-of-page), [3.1.2 Language of Parts](https://www.w3.org/TR/WCAG21/#language-of-parts) |
| **Predictable navigation** | On each new page, focus moves to the top or the form heading. Repeated regions can be skipped, the title shows progress, and browser history is respected. | [2.4.1 Bypass Blocks](https://www.w3.org/TR/WCAG21/#bypass-blocks), [2.4.2 Page Titled](https://www.w3.org/TR/WCAG21/#page-titled), [3.2.3 Consistent Navigation](https://www.w3.org/TR/WCAG21/#consistent-navigation), [3.3.2 Labels or Instructions](https://www.w3.org/TR/WCAG21/#labels-or-instructions) |
| **Labeled and grouped inputs** | Each input has a visible label, programmatically associated with it, and related choices are grouped in a `fieldset`. | [1.3.1 Info and Relationships](https://www.w3.org/TR/WCAG21/#info-and-relationships), [3.3.2 Labels or Instructions](https://www.w3.org/TR/WCAG21/#labels-or-instructions), [4.1.2 Name, Role, Value](https://www.w3.org/TR/WCAG21/#name-role-value) |
| **Helpful instructions** | Guidance for tricky questions comes before the input and doesn’t rely on color or position. | [1.3.3 Sensory Characteristics](https://www.w3.org/TR/WCAG21/#sensory-characteristics), [3.3.2 Labels or Instructions](https://www.w3.org/TR/WCAG21/#labels-or-instructions) |
| **Forgiving validation** | Errors appear next to the question that needs fixing and are announced to assistive technology, whether validation happens on submit or live. | [3.3.1 Error Identification](https://www.w3.org/TR/WCAG21/#error-identification), [3.3.3 Error Suggestion](https://www.w3.org/TR/WCAG21/#error-suggestion), [3.3.4 Error Prevention (Legal, Financial, Data)](https://www.w3.org/TR/WCAG21/#error-prevention-legal-financial-data), [4.1.3 Status Messages](https://www.w3.org/TR/WCAG21/#status-messages) |

Custom components follow the same rules. Alerts use programmatic status messages. Expandable content announces its expanded or collapsed state and is hidden from screen readers when collapsed. Option lists can be navigated with the arrow keys. Focus only moves when the user starts a change of context, such as following a jump link.

### Design system and state theming

Accessibility decisions live in shared components and tokens rather than on individual screens, so fixes carry across both apps and every state.

The **USWDS** is the base for layout and components in both Figma and code. **Design tokens** for color and typography are managed in Figma with Tokens Studio and generated into code to drive each state’s theme (for example CO and DC). The **`@sebt/design-system`** package wraps USWDS primitives with the correct ARIA wiring and is used by both the Portal (`SEBT.Portal.Web`) and the Enrollment Checker (`SEBT.EnrollmentChecker.Web`).

State color palettes, such as Colorado’s state design system palettes, are mapped to roles like primary, secondary, and accent. Each combination is reviewed for contrast before it becomes a token, and accent colors are only used in roles where they’ve passed that check. Designers favor dark brand tones on slightly off-white backgrounds to avoid glare.

Base text styles set a 16px minimum and 19px standard size, line and paragraph spacing of at least 1.5×, and no fully justified text. Colorado’s theme uses [**Atkinson Hyperlegible**](https://www.brailleinstitute.org/freefont/), a typeface designed for clear, distinguishable characters.

### Rules for engineers

| Rule | Why |
| --- | --- |
| **Use USWDS and `@sebt/design-system` components first** | They already meet the baseline. Reusing them is the cheapest way to stay compliant. |
| **Add ARIA labels and roles when composing something new** | Raw markup doesn’t inherit a primitive’s semantics. |
| **Keep focus states visible** | Removing an outline without replacing it is a defect. |
| **Use design tokens, never hardcoded colors** | Tokens are contrast-tested and themeable per state. Hex literals bypass both and are the usual cause of contrast regressions. This applies to the documentation site too. |

## Tooling in the repository

| Tool | What it does | In CI? |
| --- | --- | --- |
| **pa11y-ci** (Portal) | WCAG 2 AA scan using the [axe](https://www.deque.com/axe/axe-core/) and [HTML_CodeSniffer](https://squizlabs.github.io/HTML_CodeSniffer/) runners | Yes (`a11y` job in `playwright-e2e.yaml`) |
| **pa11y-ci** (Enrollment Checker) | WCAG 2 AA scan using the axe runner | No (run manually) |
| **axe-core runtime monitoring** | Logs violations (color contrast, landmarks, regions) to the browser console while you develop | No (development only, removed from production builds) |
| **Playwright E2E** | Selects elements by role and accessible name, so a missing or wrong name usually breaks a test | Yes (indirect coverage, not a substitute for accessibility testing) |

pa11y is configured in the Portal's `.pa11yci.json` and the Enrollment Checker's `.pa11yci`. Runtime axe monitoring is set up in `src/lib/axe.ts`.

The Portal’s `a11y` CI job runs against the full stack, with a real API and web server, and runs whenever backend or frontend code changes. The runtime axe monitoring is there to catch problems while you’re working. It doesn’t block merges.

### Running checks locally

```bash
# Portal: from apps/portal/src/SEBT.Portal.Web
pnpm test:a11y

# Portal: from the repo root (same entry point CI uses)
pnpm ci:test:a11y

# Enrollment Checker (not yet in CI; run before merging changes)
pnpm --filter @sebt/enrollment-checker test:a11y
```

Pre-commit hooks also run ESLint, Prettier, TypeScript, and token regeneration, which keeps generated theme tokens in sync with the design source.

## Manual review and UAT

Automated tooling and code scanning may catch some accessibility issues and help to find regressions but we still recommend manual accessibility testing with screen readers and keyboards. Manual review fills the gaps that automated tools can’t reach, such as keyboard navigation, screen reader behavior, logical focus order, and whether error messages make sense. The client form accessibility principles above are the main rubric for reviewing form flows.

Testing with different screen readers like JAWS, NVDA and VoiceOver as well as different screen reader and browser combinations can be done using [Assistiv Labs](https://assistivlabs.com/).

## Further reading

**Standards and legal context**

- [WCAG 2.1](https://www.w3.org/TR/WCAG21/), [Understanding WCAG 2.1](https://www.w3.org/WAI/WCAG21/Understanding/), and [How to Meet WCAG (Quick Reference)](https://www.w3.org/WAI/WCAG21/quickref/)
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [ADA.gov: Fact sheet on the Title II web and mobile accessibility rule](https://www.ada.gov/resources/2024-03-08-web-rule/)
- [Section508.gov](https://www.section508.gov/)

**Design and patterns**

- [USWDS: Accessibility](https://designsystem.digital.gov/documentation/accessibility/) and [USWDS: Design tokens](https://designsystem.digital.gov/design-tokens/)
- [W3C WAI: Forms tutorial](https://www.w3.org/WAI/tutorials/forms/)
- [WAI-ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/)
- [Atkinson Hyperlegible (Braille Institute)](https://www.brailleinstitute.org/freefont/)
- [Tokens Studio documentation](https://docs.tokens.studio/)

**Testing**

- [pa11y](https://pa11y.org/) and [pa11y-ci](https://github.com/pa11y/pa11y-ci)
- [axe-core](https://github.com/dequelabs/axe-core) and [axe rule descriptions](https://dequeuniversity.com/rules/axe/)
- [Playwright: Accessibility testing](https://playwright.dev/docs/accessibility-testing)
- [Lighthouse accessibility scoring](https://developer.chrome.com/docs/lighthouse/accessibility/scoring)
- [WebAIM: Contrast checker](https://webaim.org/resources/contrastchecker/) and [WebAIM: Testing with screen readers](https://webaim.org/articles/screenreader_testing/)
- [BrowserStack accessibility testing](https://www.browserstack.com/accessibility-testing)
