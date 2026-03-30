# Enrollment Checker Figma Fidelity Fixes — Design Spec

Date: 2026-03-16

## Background

A visual comparison of the enrollment checker app against the [Figma designs](https://www.figma.com/design/32kDQ73MSbUNdAsVi8fQzF/CO-11-3-25-%F0%9F%9F%A2-Enrollment-Checker---Self-service-Portal--WORKING-?node-id=2001-4310&p=f&m=dev) revealed significant fidelity gaps. The full comparison is documented in [docs/content/enrollment-checker-figma-comparison.md](../../content/enrollment-checker-figma-comparison.md).

This spec covers four workstreams to close those gaps, plus a verification pass.

## Workstream 1: i18n Section Filtering

### Problem

`generate-locales.js` maps both S1 (enrollment checker) and S3 (portal profile management) to the `personalInfo` namespace via the `sectionToNamespace` config. Same collision affects `confirmInfo`. Because S3 rows appear later in the CSV and the collision resolution uses "last one wins" logic, the portal's values overwrite the enrollment checker's.

Result: headings like "Are you sure you want to delete this address?" appear on the enrollment checker's child form page instead of "Check if your child is already enrolled in Summer EBT".

### Existing filtering mechanism

The script already has an `--app` CLI flag that filters at the **namespace** level. The enrollment checker passes `--app enrollment`, and the `NAMESPACE_APP` mapping controls which namespaces are included. However, `personalInfo` and `confirmInfo` are mapped as `'all'` (shared between portal and enrollment), so the `--app` filter lets both S1 and S3 rows through for these namespaces.

### Solution

Add a `--sections` CLI flag to `generate-locales.js` that filters at the **CSV row** level, complementing the existing `--app` namespace-level filter. When `--sections` is provided, only rows from the specified CSV sections are processed. This is a different filtering stage:
- `--app` controls **which namespaces** appear in the output
- `--sections` controls **which CSV rows** contribute to those namespaces

### Changes

- **`packages/design-system/content/scripts/generate-locales.js`**
  - Parse `--sections` CLI arg (comma-separated list, e.g., `S1,GLOBAL`)
  - In `buildStateLocaleData()`, after `parseContentKey()` extracts the section from a row, skip the row if its section is not in the allowed list
  - When `--sections` is not provided, all sections are processed (backward compatible)
  - The filtering happens at the row level before namespace mapping, so no changes to `sectionToNamespace` or `pageToNamespace` are needed

- **`src/SEBT.EnrollmentChecker.Web/package.json`**
  - Update `copy:generate` script from:
    ```
    node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app enrollment
    ```
    to:
    ```
    node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app enrollment --sections S1,GLOBAL
    ```

- **Regenerate locale files**
  - Run `pnpm copy:generate` in the enrollment checker after the script change
  - Verify `personalInfo.title` = "Check if your child is already enrolled in Summer EBT"
  - Verify `confirmInfo.title` = "Here's the information we have so far"

- **`docs/adr/0009-locale-section-filtering.md`**
  - ADR documenting the decision

### ADR-0009 outline

- **Context:** Multiple CSV sections map to the same i18n namespace. The enrollment checker and main portal need different values for the same namespace keys.
- **Decision:** Add `--sections` flag to `generate-locales.js` so each app can filter to its relevant CSV sections.
- **Alternatives considered:** (1) Separate CSV files per app — creates content management overhead. (2) Rename S1 namespaces (e.g., `enrollmentPersonalInfo`) — breaks predictable naming convention and requires changing all component `useTranslation()` calls.
- **Consequences:** Each app's `copy:generate` script specifies which sections it consumes. Content authors continue using the shared CSV. New apps must declare their section filter.

## Workstream 2: Rich Text Rendering

### Problem

Locale strings contain markdown formatting (`**bold**`, `\n` paragraph breaks) but the project has no way to render them. They display as raw text with literal asterisks.

### Solution

Add `markdown-to-jsx` (zero-dependency, ~18KB) to `@sebt/design-system`. Create a `RichText` wrapper component. This approach avoids unsafe HTML injection — `markdown-to-jsx` produces React elements directly, never raw HTML strings.

### Component design

```tsx
// packages/design-system/src/components/RichText/RichText.tsx
import Markdown from 'markdown-to-jsx'

interface RichTextProps {
  children: string
  inline?: boolean  // forceInline mode — no wrapping <p> tags
}

export function RichText({ children, inline = false }: RichTextProps) {
  return (
    <Markdown options={{
      ...(inline && { forceInline: true }),
      overrides: {}  // extensible — can add link, heading overrides later
    }}>
      {children}
    </Markdown>
  )
}
```

### Usage pattern

- **Plain text:** `{t('key')}` (unchanged — the majority of strings)
- **Inline markdown** (bold within a sentence): `<RichText inline>{t('key')}</RichText>`
- **Multi-paragraph markdown:** `<RichText>{t('key')}</RichText>`

### Changes

- **`packages/design-system/package.json`** — Add `markdown-to-jsx` dependency
- **`packages/design-system/src/components/RichText/RichText.tsx`** — New component
- **`packages/design-system/src/components/RichText/RichText.test.tsx`** — Unit tests (renders bold, handles `\n`, inline mode)
- **`packages/design-system/src/index.ts`** — Export `RichText`
- **`docs/adr/0010-rich-text-rendering.md`** — ADR documenting the decision

### ADR-0010 outline

- **Context:** Locale strings from state CSVs contain markdown-style formatting. The project needs a way to render these as styled React elements safely.
- **Decision:** Add `markdown-to-jsx` to the design-system package, wrapped in a `RichText` component. Use at render time — no transformation of CSVs or generated JSON files. What content authors write in the CSV = what's in the JSON = what the component receives.
- **Alternatives considered:** (1) Transform markdown to HTML tags at generation time in `generate-locales.js`, then use i18next `<Trans>` component — adds hidden transformation layer, JSON files become less human-readable, couples generation script to specific markdown patterns. (2) `react-markdown` — full CommonMark renderer with remark/rehype ecosystem (~60KB, 10+ transitive deps), significantly overpowered for our needs.
- **Consequences:** New `RichText` component available to both portal and enrollment checker. Components must opt in by wrapping `t()` calls in `<RichText>`. No changes to the content pipeline.

## Workstream 3: Image Assets

### Problem

CO state images (header logo, footer seal, translate icon) return 404 in the enrollment checker. Additional assets are needed for the SUMMER EBT logo and form card graphic.

### Solution

Copy existing SVGs from the portal into the enrollment checker. Extract missing assets from Figma.

### Changes

- **Create directory structure:**
  ```
  src/SEBT.EnrollmentChecker.Web/public/images/states/co/
  src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons/
  src/SEBT.EnrollmentChecker.Web/public/images/states/dc/
  src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons/
  ```

- **Copy from portal** (`src/SEBT.Portal.Web/public/images/states/`):
  - `co/logo.svg` — CDHS header logo
  - `co/seal.svg` — Colorado footer seal
  - `co/icons/translate_Rounded.svg` — Language selector icon
  - `dc/logo.svg`, `dc/seal.svg`, `dc/icons/translate_Rounded.svg` — DC equivalents

- **Extract from Figma:**
  - SUMMER EBT logo (between header and heading on landing page)
  - Form card graphic / clipboard icon (above heading on form and review pages)
  - If these are available as USWDS icons or already exist in the USWDS assets, use those instead of extracting from Figma

## Workstream 4: Structural Fidelity Fixes

### Landing Page (`LandingPage.tsx`)

- Add SUMMER EBT logo image between header and heading
- Wrap body text in `<RichText>` for bold formatting support
- Change CTA from "Continue" (`cta` key) to "Apply now" (`action` key)
- Add Spanish CTA button "Aplica ahora" (`actionEspañol` key), outline variant
- Add expandable accordion FAQ section using USWDS accordion classes:
  - Accordion title from `accordionTitle` key
  - Content from `body2`, `body3`, `body4` keys
  - `body3` content is `\n`-delimited — split on `\n`, filter empty strings, render as `<ul>` with `<li>` items (custom split-and-map logic in the component, not handled by `RichText`, since the content uses `\n` delimiters rather than markdown list syntax)
  - Follow existing accordion pattern in `src/SEBT.Portal.Web/src/features/household/components/EbtEdgeSection/EbtEdgeSection.tsx` for USWDS accordion markup and `aria-expanded`/`aria-controls` accessibility

### Disclaimer Page (`DisclaimerPage.tsx`)

- Replace single `{t('body')}` paragraph with structured two-paragraph layout:
  - Paragraph 1: Bold lead sentence (`body1`) followed by regular text (`body2`)
  - Paragraph 2: Bold lead sentence (`body3`) followed by regular text (`body4`)
- Use `<RichText>` or explicit `<strong>` tags for bold leads

### Child Form Page (`ChildFormPage.tsx`)

- Add description text below heading (`body` key from `personalInfo` namespace)
- Add "Asterisks (*) indicate a required field" text (`requiredFields` key from `common` namespace)
- Add form card graphic / clipboard icon above heading

### Child Form (`ChildForm.tsx`)

- **Birthdate field:** Replace single text input with three-field date input:
  - Month: `<select>` dropdown with month names (January–December), using `common` namespace month keys
  - Day: text `<input>` (numeric, 2 digits)
  - Year: text `<input>` (numeric, 4 digits)
  - Wrap all three in a USWDS `usa-memorable-date` fieldset pattern for accessibility
  - The month dropdown is an intentional UX choice to prevent MM/DD transposition across cultures

- **Field hints:** Add hint text under first name and last name fields:
  - "Legally as it appears on official documents, e.g. birth certificate" (`legallyAsItAppears` key from `common`)

- **Cancel to Back:** Change cancel button label to use `back` key from `common` (instead of `cancel`)

- **Zod schema update:** `childSchema.ts` — Replace `dateOfBirth: z.string()` with separate fields:
  ```ts
  month: z.string().min(1),   // "1" through "12"
  day: z.string().regex(/^\d{1,2}$/),
  year: z.string().regex(/^\d{4}$/)
  ```
  Add a `.transform()` that composes these into a `dateOfBirth` ISO string (`YYYY-MM-DD`) for API submission. The `Child` type in `EnrollmentContext` continues to store `dateOfBirth` as a string — the composition happens at form submission via the Zod transform, before the value enters context. The `ChildFormValues` type (form-level) will have `month`, `day`, `year`; the `Child` type (context-level) keeps `dateOfBirth`.

### Review Page (`ReviewPage.tsx`)

- Add form card graphic / clipboard icon above heading
- Add description paragraph below heading (content from `body` key in `confirmInfo` namespace — will have correct value after section filtering fix)
- **Restructure child cards:**
  - Add "Name" label above the formatted name (`tableNameHeading` key)
  - Add "Birthdate" label above the formatted date (`tableBirthdateHeading` key)
  - Format date as "[Month], [Day] [Year]" instead of raw ISO date
  - Replace "Edit" / "Remove" buttons with "Update this child's information" link (`tableAction` key). The Figma design does not show a remove button on the review page — removal is intentionally omitted. Remove the `onRemove` prop from `ChildReviewCard` and the `removeChild` call from `ReviewPage`. The `removeChild` function stays in `EnrollmentContext` for potential future use but is no longer invoked from the review page UI.
  - Add horizontal rule (`<hr>`) separators between child cards
- **Restructure button layout:**
  - "Back" (outline) + "Submit" (filled) as a button group row
  - "Add another child" as a text link below the buttons (not an outline button)

### Test Updates

- **`ChildForm.test.tsx`** — Update to interact with month dropdown + day/year text inputs instead of single birthdate field
- **`e2e/enrollment.spec.ts`** — Update to select month from dropdown and fill day/year fields
- **Run full test suite** after all changes to confirm no regressions

## Workstream 5: Verification Pass

After all implementation is complete:

1. Start the dev server (`pnpm dev` in enrollment checker)
2. Use Playwright to navigate through each page at 375px mobile width (matching Figma viewport)
3. Take full-page screenshots of each screen:
   - Landing page (`/`)
   - Disclaimer (`/disclaimer`)
   - Child form (`/check`)
   - Review page (`/review`) — with a child added
4. Get Figma screenshots of the corresponding design frames
5. Compare side-by-side and document any remaining discrepancies
6. If issues are found, iterate with targeted fixes

## Out of Scope

The following items are known gaps but deferred from this work:

- **Results page variants** — The Figma shows 5 result screen variants (Streamlined Enrolled, No Results, Apply for SEBT, Mixed, Error). These require backend API integration to test and are deferred until the API is connected.
- **Centralized asset management** — Image assets are copied directly into the enrollment checker (matching the portal's pattern). A future refactor could move state-specific assets into the design-system package with a copy script. See Workstream 3.
- **Missing CSV keys** — 20 locale keys used by enrollment checker components are not present in the state CSVs. These are documented in `docs/content/enrollment-checker-missing-csv-keys.md` and depend on content team action.

## Files Changed (Summary)

### New files
- `packages/design-system/src/components/RichText/RichText.tsx`
- `packages/design-system/src/components/RichText/RichText.test.tsx`
- `docs/adr/0009-locale-section-filtering.md`
- `docs/adr/0010-rich-text-rendering.md`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/co/logo.svg`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/co/seal.svg`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons/translate_Rounded.svg`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/logo.svg`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/seal.svg`
- `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons/translate_Rounded.svg`
- SUMMER EBT logo asset(s) — exact path TBD based on Figma extraction
- Form card graphic asset — exact path TBD

### Modified files
- `packages/design-system/content/scripts/generate-locales.js`
- `packages/design-system/package.json`
- `packages/design-system/src/index.ts`
- `src/SEBT.EnrollmentChecker.Web/package.json`
- `src/SEBT.EnrollmentChecker.Web/content/locales/` (regenerated)
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts`
- `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts`
