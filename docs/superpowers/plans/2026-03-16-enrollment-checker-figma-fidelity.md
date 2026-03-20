# Enrollment Checker Figma Fidelity Fixes — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close visual fidelity gaps between the enrollment checker app and Figma designs across landing, disclaimer, child form, and review pages.

**Architecture:** Four workstreams — i18n section filtering, rich text rendering, image assets, and structural component fixes — followed by a verification pass. Foundation work (workstreams 1-3) can run in parallel; structural fixes (workstream 4) depend on workstreams 1-2.

**Tech Stack:** Next.js 16, React 19, TypeScript, Zod, i18next, USWDS 3.13, markdown-to-jsx, Vitest, Playwright

**Spec:** `docs/superpowers/specs/2026-03-16-enrollment-checker-figma-fidelity-design.md`

---

## File Structure

### New files
| File | Responsibility |
|------|---------------|
| `packages/design-system/src/components/RichText/RichText.tsx` | Markdown-to-React rendering wrapper |
| `packages/design-system/src/components/RichText/RichText.test.tsx` | Unit tests for RichText |
| `docs/adr/0009-locale-section-filtering.md` | ADR for `--sections` flag |
| `docs/adr/0010-rich-text-rendering.md` | ADR for markdown-to-jsx approach |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/co/logo.svg` | CDHS header logo (copy from portal) |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/co/seal.svg` | Colorado footer seal (copy from portal) |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons/translate_Rounded.svg` | Translate icon (copy from portal) |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/logo.svg` | DC header logo (copy from portal) |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/seal.svg` | DC footer seal (copy from portal) |
| `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons/translate_Rounded.svg` | DC translate icon (copy from portal) |

### Modified files
| File | Changes |
|------|---------|
| `packages/design-system/content/scripts/generate-locales.js` | Add `--sections` CLI flag for row-level filtering |
| `packages/design-system/package.json` | Add `markdown-to-jsx` dependency |
| `packages/design-system/src/index.ts` | Export `RichText` and `RichTextProps` |
| `src/SEBT.EnrollmentChecker.Web/package.json` | Add `--sections S1,GLOBAL` to `copy:generate` script |
| `src/SEBT.EnrollmentChecker.Web/content/locales/` | Regenerated locale files |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts` | Split `dateOfBirth` into `month`/`day`/`year` + transform |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx` | Decouple `Child` from `ChildFormValues`, add date conversion |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx` | Three-field birthdate, field hints, Back button |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx` | Update tests for new birthdate fields |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx` | Add description text, required fields hint |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx` | Logo, RichText, CTAs, accordion |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx` | Structured body1-body4 paragraphs |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx` | Button layout, description, remove onRemove usage |
| `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx` | Name/Birthdate labels, formatted date, update link |
| `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts` | Update for new birthdate fields, button labels |

---

## Chunk 1: Foundation (Tasks 1-3 — parallelizable)

### Task 1: Add `--sections` CLI flag to `generate-locales.js`

**Files:**
- Modify: `packages/design-system/content/scripts/generate-locales.js:68` (CLI parsing), `:321-331` (row filtering)
- Modify: `src/SEBT.EnrollmentChecker.Web/package.json:10` (copy:generate script)
- Create: `docs/adr/0009-locale-section-filtering.md`

- [ ] **Step 1: Add `--sections` CLI arg parsing**

In `packages/design-system/content/scripts/generate-locales.js`, after line 68 (`const appFilter = ...`), add:

```js
const sectionsFilter = getCliArg('--sections')  // comma-separated, e.g., 'S1,GLOBAL'
const allowedSections = sectionsFilter ? sectionsFilter.split(',').map(s => s.trim()) : null
```

- [ ] **Step 2: Add section filtering in `buildStateLocaleData()`**

In the same file, inside `buildStateLocaleData()`, after line 330 (`if (!parsed) continue;`), add:

```js
    // Section-level filter: skip rows from sections not in the allowed list
    if (allowedSections && !allowedSections.includes(parsed.section)) continue;
```

This goes right before `const { namespace, key } = parsed;` (line 332).

- [ ] **Step 3: Update enrollment checker `copy:generate` script**

In `src/SEBT.EnrollmentChecker.Web/package.json`, change the `copy:generate` script from:

```
node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app enrollment
```

to:

```
node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app enrollment --sections S1,GLOBAL
```

- [ ] **Step 4: Regenerate locale files and verify**

Run:
```bash
cd src/SEBT.EnrollmentChecker.Web
pnpm copy:generate
```

Verify:
- `content/locales/en/co/personalInfo.json` → `title` should be "Check if your child is already enrolled in Summer EBT"
- `content/locales/en/co/confirmInfo.json` → `title` should be "Here's the information we have so far"

- [ ] **Step 5: Write ADR-0009**

Create `docs/adr/0009-locale-section-filtering.md`:

```markdown
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
```

- [ ] **Step 6: Commit**

```bash
git add packages/design-system/content/scripts/generate-locales.js
git add src/SEBT.EnrollmentChecker.Web/package.json
git add src/SEBT.EnrollmentChecker.Web/content/locales/
git add docs/adr/0009-locale-section-filtering.md
git commit -m "feat: add --sections flag to generate-locales.js for row-level filtering

Fixes i18n namespace collision where portal S3 rows overwrote
enrollment checker S1 values for personalInfo and confirmInfo.

ADR-0009 documents the decision."
```

---

### Task 2: Add `RichText` component to design-system

**Files:**
- Modify: `packages/design-system/package.json` (add dependency)
- Create: `packages/design-system/src/components/RichText/RichText.tsx`
- Create: `packages/design-system/src/components/RichText/RichText.test.tsx`
- Modify: `packages/design-system/src/index.ts` (export)
- Create: `docs/adr/0010-rich-text-rendering.md`

- [ ] **Step 1: Install `markdown-to-jsx`**

```bash
cd packages/design-system
pnpm add markdown-to-jsx
```

- [ ] **Step 2: Write failing tests**

Create `packages/design-system/src/components/RichText/RichText.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RichText } from './RichText'

describe('RichText', () => {
  it('renders plain text unchanged', () => {
    render(<RichText>Hello world</RichText>)
    expect(screen.getByText('Hello world')).toBeInTheDocument()
  })

  it('renders bold markdown as <strong>', () => {
    const { container } = render(<RichText>This is **bold** text</RichText>)
    const strong = container.querySelector('strong')
    expect(strong).toBeInTheDocument()
    expect(strong?.textContent).toBe('bold')
  })

  it('renders newline-separated paragraphs as separate <p> tags', () => {
    const { container } = render(<RichText>{'Paragraph one\n\nParagraph two'}</RichText>)
    const paragraphs = container.querySelectorAll('p')
    expect(paragraphs).toHaveLength(2)
    expect(paragraphs[0].textContent).toBe('Paragraph one')
    expect(paragraphs[1].textContent).toBe('Paragraph two')
  })

  it('renders inline mode without wrapping <p> tags', () => {
    const { container } = render(<RichText inline>This is **bold** inline</RichText>)
    // In inline mode, there should be no <p> wrapper
    expect(container.querySelector('p')).not.toBeInTheDocument()
    expect(container.querySelector('strong')?.textContent).toBe('bold')
  })
})
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
cd packages/design-system
pnpm test -- --run src/components/RichText/RichText.test.tsx
```

Expected: FAIL — module `./RichText` not found.

- [ ] **Step 4: Implement `RichText` component**

Create `packages/design-system/src/components/RichText/RichText.tsx`:

```tsx
import Markdown from 'markdown-to-jsx'

export interface RichTextProps {
  children: string
  /** When true, renders inline (no wrapping <p> tags). Use for bold within a sentence. */
  inline?: boolean
}

export function RichText({ children, inline = false }: RichTextProps) {
  return (
    <Markdown options={{
      ...(inline && { forceInline: true }),
      overrides: {}
    }}>
      {children}
    </Markdown>
  )
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd packages/design-system
pnpm test -- --run src/components/RichText/RichText.test.tsx
```

Expected: PASS — all 4 tests green.

- [ ] **Step 6: Export from design-system index**

In `packages/design-system/src/index.ts`, after the TextLink exports (line 18), add:

```ts
// Rich text rendering (markdown-to-jsx)
export { RichText } from './components/RichText/RichText'
export type { RichTextProps } from './components/RichText/RichText'
```

- [ ] **Step 7: Write ADR-0010**

Create `docs/adr/0010-rich-text-rendering.md`:

```markdown
# 10. Rich text rendering with markdown-to-jsx

Date: 2026-03-16

## Status

Accepted

## Context

Locale strings from state CSVs contain markdown-style formatting (`**bold**`, `\n` paragraph breaks). The project needs a way to render these as styled React elements safely. Currently, bold syntax displays as literal asterisks in the UI.

## Decision

Add `markdown-to-jsx` to the design-system package, wrapped in a `RichText` component. Content is rendered at runtime — no transformation of CSVs or generated JSON files. What content authors write in the CSV is what's in the JSON is what the component receives.

Usage:
- Plain text (majority): `{t('key')}` — unchanged
- Inline markdown (bold within a sentence): `<RichText inline>{t('key')}</RichText>`
- Multi-paragraph markdown: `<RichText>{t('key')}</RichText>`

`markdown-to-jsx` produces React elements directly from markdown strings. It never injects raw HTML — the output is a React element tree, making it safe by construction.

## Alternatives Considered

1. **Transform markdown to HTML at generation time in `generate-locales.js`**, then use i18next `<Trans>` component — adds a hidden transformation layer, JSON files become less human-readable, and couples the generation script to specific markdown patterns.
2. **`react-markdown`** — full CommonMark renderer with remark/rehype ecosystem (~60KB, 10+ transitive deps). Significantly overpowered for our needs (primarily bold text and paragraph breaks).
3. **Hand-implement a custom markdown-to-React parser** — error-prone, testing burden, maintenance overhead. A well-tested library is the safer choice.

## Consequences

- New `RichText` component available to both portal and enrollment checker.
- Components must opt in by wrapping `t()` calls in `<RichText>`.
- No changes to the content pipeline.
- ~18KB added bundle size (zero transitive dependencies).
```

- [ ] **Step 8: Commit**

```bash
git add packages/design-system/src/components/RichText/
git add packages/design-system/package.json
git add packages/design-system/src/index.ts
git add docs/adr/0010-rich-text-rendering.md
git commit -m "feat: add RichText component wrapping markdown-to-jsx

Renders markdown-formatted locale strings as React elements.
Supports bold, paragraph breaks, and inline mode.

ADR-0010 documents the decision."
```

---

### Task 3: Copy image assets to enrollment checker

**Files:**
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/co/logo.svg` (copy)
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/co/seal.svg` (copy)
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons/translate_Rounded.svg` (copy)
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/logo.svg` (copy)
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/seal.svg` (copy)
- Create: `src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons/translate_Rounded.svg` (copy)

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons
mkdir -p src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons
```

- [ ] **Step 2: Copy CO assets from portal**

```bash
cp src/SEBT.Portal.Web/public/images/states/co/logo.svg src/SEBT.EnrollmentChecker.Web/public/images/states/co/logo.svg
cp src/SEBT.Portal.Web/public/images/states/co/seal.svg src/SEBT.EnrollmentChecker.Web/public/images/states/co/seal.svg
cp src/SEBT.Portal.Web/public/images/states/co/icons/translate_Rounded.svg src/SEBT.EnrollmentChecker.Web/public/images/states/co/icons/translate_Rounded.svg
```

- [ ] **Step 3: Copy DC assets from portal**

```bash
cp src/SEBT.Portal.Web/public/images/states/dc/logo.svg src/SEBT.EnrollmentChecker.Web/public/images/states/dc/logo.svg
cp src/SEBT.Portal.Web/public/images/states/dc/seal.svg src/SEBT.EnrollmentChecker.Web/public/images/states/dc/seal.svg
cp src/SEBT.Portal.Web/public/images/states/dc/icons/translate_Rounded.svg src/SEBT.EnrollmentChecker.Web/public/images/states/dc/icons/translate_Rounded.svg
```

- [ ] **Step 4: Verify assets load**

Start the dev server and confirm no 404 errors for image paths in the browser console.

- [ ] **Step 5: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/public/images/states/
git commit -m "feat: add state image assets to enrollment checker

Copies CDHS logo, state seal, and translate icon from portal
for both CO and DC. Fixes broken images in header and footer."
```

---

## Chunk 2: Schema & Form (Tasks 4-6 — sequential)

### Task 4: Split birthdate into month/day/year in Zod schema

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts`
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx`

- [ ] **Step 1: Rewrite `childSchema.ts`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts`:

```ts
import { z } from 'zod'

/**
 * Form-level schema: the shape of data as it lives in the ChildForm UI.
 * Month/day/year are separate fields for the USWDS memorable-date pattern.
 */
export const childFormSchema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional(),
  lastName: z.string().min(1, 'Last name is required').max(100),
  month: z.string().min(1, 'Month is required'),
  day: z.string().regex(/^\d{1,2}$/, 'Day must be 1-2 digits'),
  year: z.string().regex(/^\d{4}$/, 'Year must be 4 digits'),
  schoolName: z.string().max(200).optional(),
  schoolCode: z.string().max(50).optional()
})

export type ChildFormValues = z.infer<typeof childFormSchema>

/** Compose month/day/year into an ISO date string (YYYY-MM-DD). */
export function toDateOfBirth(values: Pick<ChildFormValues, 'month' | 'day' | 'year'>): string {
  const mm = values.month.padStart(2, '0')
  const dd = values.day.padStart(2, '0')
  return `${values.year}-${mm}-${dd}`
}

/** Decompose an ISO date string into month/day/year for form population. */
export function fromDateOfBirth(dateOfBirth: string): { month: string; day: string; year: string } {
  const [year, month, day] = dateOfBirth.split('-')
  // Strip leading zeros for natural display (e.g., "04" -> "4")
  return {
    month: String(parseInt(month, 10)),
    day: String(parseInt(day, 10)),
    year
  }
}
```

- [ ] **Step 2: Update `EnrollmentContext.tsx`**

In `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx`:

**a)** Update imports (line 6-7):

Replace:
```ts
import { childSchema } from '../schemas/childSchema'
import type { ChildFormValues } from '../schemas/childSchema'
```

With:
```ts
import { childFormSchema, toDateOfBirth } from '../schemas/childSchema'
import type { ChildFormValues } from '../schemas/childSchema'
```

**b)** Replace the `Child` interface (line 11-13):

Replace:
```ts
export interface Child extends ChildFormValues {
  id: string
}
```

With:
```ts
export interface Child {
  id: string
  firstName: string
  middleName?: string
  lastName: string
  dateOfBirth: string  // ISO date: YYYY-MM-DD
  schoolName?: string
  schoolCode?: string
}
```

**c)** Update `childStorageSchema` (line 44):

Replace:
```ts
const childStorageSchema = childSchema.extend({ id: z.string() })
```

With:
```ts
const childStorageSchema = z.object({
  id: z.string(),
  firstName: z.string(),
  middleName: z.string().optional(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  schoolName: z.string().optional(),
  schoolCode: z.string().optional()
})
```

**d)** Update `addChild` action (line 90-93):

Replace:
```ts
    addChild: (values) => update(s => ({
      ...s,
      children: [...s.children, { id: uuidv4(), ...values }]
    })),
```

With:
```ts
    addChild: (values) => update(s => ({
      ...s,
      children: [...s.children, {
        id: uuidv4(),
        firstName: values.firstName,
        middleName: values.middleName,
        lastName: values.lastName,
        dateOfBirth: toDateOfBirth(values),
        schoolName: values.schoolName,
        schoolCode: values.schoolCode
      }]
    })),
```

**e)** Update `updateChild` action (line 94-97):

Replace:
```ts
    updateChild: (id, values) => update(s => ({
      ...s,
      children: s.children.map(c => c.id === id ? { id, ...values } : c)
    })),
```

With:
```ts
    updateChild: (id, values) => update(s => ({
      ...s,
      children: s.children.map(c => c.id === id ? {
        id,
        firstName: values.firstName,
        middleName: values.middleName,
        lastName: values.lastName,
        dateOfBirth: toDateOfBirth(values),
        schoolName: values.schoolName,
        schoolCode: values.schoolCode
      } : c)
    })),
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd src/SEBT.EnrollmentChecker.Web
npx tsc --noEmit
```

Expected: Type errors in `ChildForm.tsx` and `ChildForm.test.tsx` (because they still reference the old `dateOfBirth` field). This is expected — we'll fix those in Tasks 5-6.

- [ ] **Step 4: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx
git commit -m "refactor: split birthdate into month/day/year in schema and context

ChildFormValues now has separate month, day, year fields for the
USWDS memorable-date form pattern. Child type stores composed
dateOfBirth as ISO string. Conversion helpers bridge the two."
```

---

### Task 5: Rewrite `ChildForm` with three-field birthdate

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx`
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx`

- [ ] **Step 1: Write updated tests**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { ChildForm } from './ChildForm'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('ChildForm', () => {
  it('renders required fields including month dropdown and day/year inputs', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByRole('textbox', { name: /first name/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /last name/i })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: /month/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /day/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /year/i })).toBeInTheDocument()
  })

  it('renders hint text for first name and last name', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByText(/legally as it appears/i)).toBeInTheDocument()
  })

  it('does not render school field when showSchoolField is false', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    // SchoolSelect renders a combobox when enabled. The month dropdown is the only combobox otherwise.
    const comboboxes = screen.getAllByRole('combobox')
    expect(comboboxes).toHaveLength(1) // only the month dropdown
  })

  it('shows validation error on submit when firstName is empty', async () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/first name is required/i)).toBeInTheDocument()
  })

  it('calls onSubmit with valid values including separate date fields', async () => {
    const onSubmit = vi.fn()
    render(<ChildForm onSubmit={onSubmit} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), '4')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2015')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
    )
  })

  it('uses Back label instead of Cancel for the cancel button', () => {
    render(<ChildForm onSubmit={vi.fn()} onCancel={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd src/SEBT.EnrollmentChecker.Web
pnpm test -- --run src/features/enrollment/components/ChildForm.test.tsx
```

Expected: FAIL — no combobox with name "month", no textbox with name "day"/"year".

- [ ] **Step 3: Rewrite `ChildForm.tsx`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx`:

```tsx
'use client'

import { InputField } from '@sebt/design-system'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { childFormSchema, fromDateOfBirth } from '../schemas/childSchema'
import { SchoolSelect } from './SchoolSelect'

interface ChildFormProps {
  initialValues?: Child
  onSubmit: (values: ChildFormValues) => void
  onCancel?: () => void
  showSchoolField: boolean
  apiBaseUrl: string
}

const MONTHS = [
  { value: '1', label: 'January' },
  { value: '2', label: 'February' },
  { value: '3', label: 'March' },
  { value: '4', label: 'April' },
  { value: '5', label: 'May' },
  { value: '6', label: 'June' },
  { value: '7', label: 'July' },
  { value: '8', label: 'August' },
  { value: '9', label: 'September' },
  { value: '10', label: 'October' },
  { value: '11', label: 'November' },
  { value: '12', label: 'December' },
]

export function ChildForm({
  initialValues,
  onSubmit,
  onCancel,
  showSchoolField,
  apiBaseUrl
}: ChildFormProps) {
  const { t } = useTranslation('personalInfo')
  const { t: tCommon } = useTranslation('common')

  // If editing, decompose the stored dateOfBirth into month/day/year
  const initialDate = initialValues?.dateOfBirth
    ? fromDateOfBirth(initialValues.dateOfBirth)
    : { month: '', day: '', year: '' }

  const [values, setValues] = useState<Partial<ChildFormValues>>({
    firstName: initialValues?.firstName ?? '',
    middleName: initialValues?.middleName ?? '',
    lastName: initialValues?.lastName ?? '',
    month: initialDate.month,
    day: initialDate.day,
    year: initialDate.year,
    schoolName: initialValues?.schoolName,
    schoolCode: initialValues?.schoolCode
  })
  const [errors, setErrors] = useState<Partial<Record<keyof ChildFormValues, string>>>({})

  function set(field: keyof ChildFormValues, value: string) {
    setValues(v => ({ ...v, [field]: value }))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const result = childFormSchema.safeParse(values)
    if (!result.success) {
      const fieldErrors: Partial<Record<keyof ChildFormValues, string>> = {}
      for (const issue of result.error.issues) {
        const key = issue.path[0] as keyof ChildFormValues
        if (!fieldErrors[key]) fieldErrors[key] = issue.message
      }
      setErrors(fieldErrors)
      return
    }
    setErrors({})
    onSubmit(result.data)
  }

  const nameHint = tCommon('legallyAsItAppears')

  return (
    <form onSubmit={handleSubmit} noValidate>
      <InputField
        label={t('firstNameLabel')}
        value={values.firstName ?? ''}
        onChange={e => set('firstName', e.target.value)}
        error={errors.firstName}
        isRequired
        hint={nameHint}
      />
      <InputField
        label={t('middleNameLabel')}
        value={values.middleName ?? ''}
        onChange={e => set('middleName', e.target.value)}
      />
      <InputField
        label={t('lastNameLabel')}
        value={values.lastName ?? ''}
        onChange={e => set('lastName', e.target.value)}
        error={errors.lastName}
        isRequired
        hint={nameHint}
      />

      {/* USWDS memorable-date pattern: Month dropdown + Day/Year text inputs */}
      <fieldset className="usa-fieldset">
        <legend className="usa-legend">
          {t('labelBirthdate')} <abbr title="required" className="usa-hint usa-hint--required">*</abbr>
        </legend>
        <div className="usa-memorable-date">
          <div className="usa-form-group usa-form-group--month">
            <label className="usa-label" htmlFor="date-month">{t('labelMonth')}</label>
            {errors.month && <span className="usa-error-message">{errors.month}</span>}
            <select
              className={`usa-select${errors.month ? ' usa-input--error' : ''}`}
              id="date-month"
              name="month"
              aria-label={t('labelMonth')}
              value={values.month ?? ''}
              onChange={e => set('month', e.target.value)}
            >
              <option value="">{tCommon('selectOne')}</option>
              {MONTHS.map(m => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
          </div>
          <div className="usa-form-group usa-form-group--day">
            <label className="usa-label" htmlFor="date-day">{t('labelDay')}</label>
            {errors.day && <span className="usa-error-message">{errors.day}</span>}
            <input
              className={`usa-input usa-input--inline${errors.day ? ' usa-input--error' : ''}`}
              id="date-day"
              name="day"
              type="text"
              inputMode="numeric"
              maxLength={2}
              aria-label={t('labelDay')}
              value={values.day ?? ''}
              onChange={e => set('day', e.target.value)}
            />
          </div>
          <div className="usa-form-group usa-form-group--year">
            <label className="usa-label" htmlFor="date-year">{t('labelYear')}</label>
            {errors.year && <span className="usa-error-message">{errors.year}</span>}
            <input
              className={`usa-input usa-input--inline${errors.year ? ' usa-input--error' : ''}`}
              id="date-year"
              name="year"
              type="text"
              inputMode="numeric"
              maxLength={4}
              aria-label={t('labelYear')}
              value={values.year ?? ''}
              onChange={e => set('year', e.target.value)}
            />
          </div>
        </div>
      </fieldset>

      <SchoolSelect
        enabled={showSchoolField}
        apiBaseUrl={apiBaseUrl}
        value={values.schoolCode ?? ''}
        onChange={(code, name) => {
          set('schoolCode', code)
          set('schoolName', name)
        }}
      />
      <div className="usa-button-group margin-top-4">
        {onCancel && (
          <button type="button" className="usa-button usa-button--outline" onClick={onCancel}>
            {tCommon('back')}
          </button>
        )}
        <button type="submit" className="usa-button">
          {tCommon('continue')}
        </button>
      </div>
    </form>
  )
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd src/SEBT.EnrollmentChecker.Web
pnpm test -- --run src/features/enrollment/components/ChildForm.test.tsx
```

Expected: PASS — all 6 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx
git commit -m "feat: replace single birthdate input with month/day/year fields

Uses USWDS memorable-date pattern with month dropdown to
prevent MM/DD transposition. Adds name field hints."
```

---

### Task 6: Update `ChildFormPage` with description and required-fields hint

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx`

- [ ] **Step 1: Update `ChildFormPage.tsx`**

Replace the return JSX in `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx` (lines 41-62):

Replace:
```tsx
  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={handleCancel}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{isEditMode ? t('editHeading') : t('title')}</h1>
        <ChildForm
          initialValues={editingChild}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          showSchoolField={showSchoolField}
          apiBaseUrl={apiBaseUrl}
        />
      </div>
    </div>
  )
```

With:
```tsx
  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={handleCancel}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{isEditMode ? t('editHeading') : t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>
        <p className="usa-hint">{t('requiredFields', { ns: 'common' })}</p>
        <ChildForm
          initialValues={editingChild}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          showSchoolField={showSchoolField}
          apiBaseUrl={apiBaseUrl}
        />
      </div>
    </div>
  )
```

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd src/SEBT.EnrollmentChecker.Web
npx tsc --noEmit
```

Expected: No errors for this file.

- [ ] **Step 3: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx
git commit -m "feat: add description text and required-fields hint to child form page"
```

---

## Chunk 3: Remaining Pages (Tasks 7-10)

### Task 7: Rewrite `LandingPage` with logo, RichText, CTAs, and accordion

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx`

**Reference:** Accordion pattern from `src/SEBT.Portal.Web/src/features/household/components/EbtEdgeSection/EbtEdgeSection.tsx`

- [ ] **Step 1: Rewrite `LandingPage.tsx`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx`:

```tsx
'use client'

import { Button, RichText } from '@sebt/design-system'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const router = useRouter()
  const [isAccordionExpanded, setIsAccordionExpanded] = useState(false)

  // body3 is \n-delimited list items — split and filter empties
  const body3Items = t('body3').split('\n').filter(Boolean)

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <div className="usa-prose">
          <RichText>{t('body')}</RichText>
        </div>

        <div className="margin-top-3">
          <Button onClick={() => router.push('/disclaimer')}>
            {t('action')}
          </Button>
        </div>
        <div className="margin-top-2">
          <Button variant="outline" onClick={() => router.push('/disclaimer')}>
            {t('actionEspañol')}
          </Button>
        </div>

        {/* FAQ Accordion — follows USWDS accordion pattern */}
        <div className="usa-accordion margin-top-4">
          <h2 className="usa-accordion__heading">
            <button
              type="button"
              className="usa-accordion__button"
              aria-expanded={isAccordionExpanded}
              aria-controls="faq-content"
              onClick={() => setIsAccordionExpanded(prev => !prev)}
            >
              {t('accordionTitle')}
            </button>
          </h2>
          <div
            id="faq-content"
            className="usa-accordion__content usa-prose"
            hidden={!isAccordionExpanded}
          >
            <RichText>{t('body2')}</RichText>
            <ul className="usa-list margin-top-2">
              {body3Items.map((item, index) => (
                <li key={index}>{item}</li>
              ))}
            </ul>
            <RichText>{t('body4')}</RichText>
          </div>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Verify it compiles**

```bash
cd src/SEBT.EnrollmentChecker.Web
npx tsc --noEmit
```

- [ ] **Step 3: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx
git commit -m "feat: add RichText, CTAs, and FAQ accordion to landing page

Replaces single Continue button with Apply now / Aplica ahora.
Adds expandable FAQ section with eligibility details.
Body text now renders bold markdown via RichText."
```

---

### Task 8: Rewrite `DisclaimerPage` with structured body paragraphs

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx`

- [ ] **Step 1: Rewrite `DisclaimerPage.tsx`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx`:

```tsx
'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function DisclaimerPage() {
  const { t } = useTranslation('disclaimer')
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <div className="usa-prose">
          <p>
            <strong>{t('body1')}</strong>{' '}
            {t('body2')}
          </p>
          <p>
            <strong>{t('body3')}</strong>{' '}
            {t('body4')}
          </p>
        </div>
        <div className="margin-top-4">
          <Button variant="outline" onClick={() => router.push('/')}>
            {t('back', { ns: 'common' })}
          </Button>
          <Button onClick={() => router.push('/check')}>
            {t('continue', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Verify it compiles**

```bash
cd src/SEBT.EnrollmentChecker.Web
npx tsc --noEmit
```

- [ ] **Step 3: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx
git commit -m "feat: use structured body1-body4 paragraphs on disclaimer page

Replaces single generic body text with two paragraphs,
each with a bold lead sentence per Figma design."
```

---

### Task 9: Rewrite `ReviewPage` and `ChildReviewCard`

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx`
- Modify: `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx`

- [ ] **Step 1: Rewrite `ChildReviewCard.tsx`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx`:

```tsx
'use client'

import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'

interface ChildReviewCardProps {
  child: Child
  onEdit: (id: string) => void
}

/** Format ISO date (YYYY-MM-DD) as "Month, Day Year" (e.g., "April, 12 2015"). */
function formatBirthdate(dateOfBirth: string): string {
  const [year, month, day] = dateOfBirth.split('-')
  const monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ]
  const monthName = monthNames[parseInt(month, 10) - 1] ?? month
  return `${monthName}, ${parseInt(day, 10)} ${year}`
}

export function ChildReviewCard({ child, onEdit }: ChildReviewCardProps) {
  const { t } = useTranslation('confirmInfo')

  const middleInitial = child.middleName ? ` ${child.middleName.charAt(0)}.` : ''
  const fullName = `${child.firstName}${middleInitial} ${child.lastName}`

  return (
    <div className="usa-card">
      <div className="usa-card__body">
        <p className="usa-prose margin-bottom-05">
          <strong>{t('tableNameHeading')}</strong>
        </p>
        <p className="usa-prose margin-top-0">{fullName}</p>
        <p className="usa-prose margin-bottom-05">
          <strong>{t('tableBirthdateHeading')}</strong>
        </p>
        <p className="usa-prose margin-top-0">{formatBirthdate(child.dateOfBirth)}</p>
        <button
          type="button"
          className="usa-button usa-button--unstyled"
          onClick={() => onEdit(child.id)}
        >
          {t('tableAction')}
        </button>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Rewrite `ReviewPage.tsx`**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx`:

```tsx
'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import { ChildReviewCard } from './ChildReviewCard'

interface ReviewPageProps {
  onSubmit: () => void
}

export function ReviewPage({ onSubmit }: ReviewPageProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { state, setEditingChildId } = useEnrollment()

  function handleEdit(id: string) {
    setEditingChildId(id)
    router.push('/check')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>

        {state.children.map((child, index) => (
          <div key={child.id}>
            {index > 0 && <hr className="margin-y-3" />}
            <ChildReviewCard child={child} onEdit={handleEdit} />
          </div>
        ))}

        <div className="usa-button-group margin-top-4">
          <Button variant="outline" onClick={() => router.push('/check')}>
            {tCommon('back')}
          </Button>
          <Button onClick={onSubmit}>
            {tCommon('submit')}
          </Button>
        </div>
        <div className="margin-top-2">
          <button
            type="button"
            className="usa-button usa-button--unstyled"
            onClick={() => {
              setEditingChildId(null)
              router.push('/check')
            }}
          >
            {tCommon('addAnotherChild')}
          </button>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd src/SEBT.EnrollmentChecker.Web
npx tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx
git commit -m "feat: restructure review page with labeled cards and updated layout

Adds Name/Birthdate labels, formatted date display, and
'Update this child's information' link per Figma. Removes
Remove button from review cards. Moves 'Add another child'
to text link below button group."
```

---

### Task 10: Update E2E tests

**Files:**
- Modify: `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts`

- [ ] **Step 1: Rewrite E2E test**

Replace the entire contents of `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts`:

```ts
import { expect, test } from '@playwright/test'

test.describe('Enrollment checker happy path', () => {
  test('navigates from landing to results', async ({ page }) => {
    // Mock the enrollment check API so we don't need a live backend
    await page.route('**/api/enrollment/check', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [{ checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }]
        })
      })
    )

    await page.goto('/')

    // Landing page — click "Apply now"
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.getByRole('button', { name: /apply now/i }).click()

    // Disclaimer page
    await expect(page.url()).toContain('/disclaimer')
    await page.getByRole('button', { name: /continue/i }).click()

    // Check page — fill three-field birthdate
    await expect(page.url()).toContain('/check')
    await page.getByRole('textbox', { name: /first name/i }).fill('Jane')
    await page.getByRole('textbox', { name: /last name/i }).fill('Doe')
    await page.getByLabel(/month/i).selectOption('4')
    await page.getByRole('textbox', { name: /day/i }).fill('12')
    await page.getByRole('textbox', { name: /year/i }).fill('2015')
    await page.getByRole('button', { name: /continue/i }).click()

    // Review page
    await expect(page.url()).toContain('/review')
    await expect(page.getByText(/Jane Doe/i)).toBeVisible()
    await expect(page.getByText(/April, 12 2015/i)).toBeVisible()
    await page.getByRole('button', { name: /submit/i }).click()

    // Results page
    await expect(page.url()).toContain('/results')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })

  test('back button returns from disclaimer to landing', async ({ page }) => {
    await page.goto('/disclaimer')
    await page.getByRole('button', { name: /back/i }).click()
    await expect(page.url()).toMatch(/\/$/)
  })

  test('/closed page renders', async ({ page }) => {
    await page.goto('/closed')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })
})
```

- [ ] **Step 2: Run E2E tests** (requires dev server running)

```bash
cd src/SEBT.EnrollmentChecker.Web
pnpm test:e2e
```

Expected: All 3 tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts
git commit -m "test: update E2E tests for new birthdate fields and button labels"
```

---

## Chunk 4: Verification (Task 11)

### Task 11: Screenshot comparison against Figma

- [ ] **Step 1: Start the enrollment checker dev server**

```bash
cd src/SEBT.EnrollmentChecker.Web
pnpm dev
```

- [ ] **Step 2: Take screenshots at 375px mobile width using Playwright**

Navigate through each screen and take full-page screenshots:
- Landing page (`/`)
- Disclaimer (`/disclaimer`)
- Child form (`/check`)
- Review page (`/review`) — with a child added

- [ ] **Step 3: Get Figma screenshots of corresponding frames**

Use Figma MCP `get_screenshot` for:
- Landing: node `6034:16454`
- Disclaimer: node `6736:19904`
- Personal Information: node `6034:16775`
- Confirm Info: node `8028:29275`

- [ ] **Step 4: Compare side-by-side and document findings**

Compare each screenshot pair. Document remaining discrepancies. If issues are found, iterate with targeted fixes.

- [ ] **Step 5: Commit any remaining fixes**

If fixes were needed, commit them with descriptive messages.

---

## Notes

- **Month names in dropdown:** The MONTHS array is hardcoded in English in `ChildForm.tsx`. The CSV has individual month keys (`january`, `august`) but not all 12. Once the content team adds the full set, the dropdown can switch to `t('january')` etc. For now, English hardcoding is acceptable since the locale files also have English month names.
- **SUMMER EBT logo and form card graphic:** These assets need to be extracted from Figma or sourced from USWDS. They are not available in the portal's existing assets. The structural components are wired up but the actual image files may need to be added separately.
- **Results page variants:** Out of scope — requires backend API integration. See spec for details.
- **Missing CSV keys:** 20 locale keys are documented in `docs/content/enrollment-checker-missing-csv-keys.md` and depend on content team action.
