---
name: conventions
description: Review code against the SEBT Self-Service Portal conventions. Use when reviewing PRs, cleaning up code, or checking if code follows our Short, Sharp, Simple principles.
argument-hint: "[file-or-directory]"
---

Review the provided code against our **Short, Sharp, Simple** principles. Reference the full conventions at [CONVENTIONS.md](../../../CONVENTIONS.md).

## Priorities (in order)

1. **It works** - baseline
2. **Short** - fewest lines possible, linear flow
3. **Sharp** - solves exactly the problem, no unnecessary generalization
4. **Simple** - if it feels complicated, stop and simplify

## What to look for

### Hardcoded display strings (critical for this project)
Every user-facing string must go through i18next. The content pipeline is Google Sheet → CSV → `generate-locales.js` → JSON. Never edit the JSON locale files directly.
- `<span>Active</span>` → `<span>{t('cardTableStatusActive')}</span>`
- `<p>Your card has been mailed.</p>` → `<p>{t('cardTableStatusMessageMailed')}</p>`
- When a DC locale key might be empty string: `t('key') || 'Fallback'` (i18next returns `''` not the fallback arg when a key exists with empty value)

### Hardcoded colors or spacing (critical for this project)
All colors and spacing must use USWDS utility classes. Never use hex values, arbitrary Tailwind values, or inline styles.
- `style={{ backgroundColor: '#00A398' }}` → `className="bg-primary-light"`
- `className="border-teal-500"` → `className="border-primary-light"`
- `className="mt-[12px]"` → `className="margin-top-2"`
- Reference semantic USWDS roles: `bg-success-dark` (active), `bg-error-dark` (inactive), `bg-warning-dark` (frozen), `bg-base-lighter` (neutral)

### Types and schemas defined outside `api/`
All Zod schemas belong in `api/schema.ts`. All derived TypeScript types belong in `api/index.ts`. Never define types in component files.
- `// ChildCard.tsx: type CardStatus = 'Active' | 'Mailed' | ...` → move to `api/schema.ts` + `api/index.ts`
- `import type { Application } from './types'` → `import type { Application } from '../../api'`

### Deep call chains
Code should read top to bottom in one place. Flag any pattern where understanding a component requires jumping through 3+ helper functions. Prefer a single `STATUS_CONFIG` lookup table over `getLabel()` + `getColor()` + `getIcon()` chains.

### Functions called only once
If a function is only called from one place, inline it unless the name adds real clarity that the expression alone doesn't give.
- `hasDcCardLifecycle(app)` is fine — `app.cardRequestedAt != null` doesn't communicate "DC card lifecycle"
- `getStatusLabelKey(uiStatus)` called once → inline the switch into the render

### Dead code
- Unused exports, types, interfaces — if nothing outside the file imports it, remove `export`
- Commented-out blocks — git has history, delete them
- Placeholder C# methods for future states — delete them, add when needed

### Wrapper components for one-time use
Don't create a component whose only job is wrapping a USWDS primitive with props passed through.
- `<CardStatusBadge status={label} />` used once → `<span className="usa-tag">{label}</span>`
- Exception: extract when used in 2+ places, or when extraction meaningfully reduces noise

### Duplicate logic across similar branches
- Two `{cardStatus === 'X' && <p>{t('key')}</p>}` blocks with the same key → combine with `||`
- Duplicate `if/else` bodies → extract the shared condition

### Verbose blocks that should be collapsed
- Single-statement `if` blocks → `if (!cardStatus) return null`
- Guard clauses that can be merged → `if (!cardStatus || cardStatus === 'Unknown') return null`

### Unnecessary section comments
Don't comment what the code obviously does.
- `// Render the badge` above `return <span>` → delete
- `// Keys map to CSV: ...` repeated above every function → one comment at the top is enough

### Exports that should be private
If a function, type, or component is only used within its own file, don't export it.
- `export type StepConfig = ...` → `type StepConfig = ...`
- `export function getBorderClass(...)` used nowhere outside → remove `export`

### C# Clean Architecture violations
Infrastructure concerns must not leak into use cases.
- `AppDbContext` referenced directly in a handler → inject `IHouseholdRepository` instead
- EF Core expressions in use case handlers → move to repository implementation

### Multiple imports from the same module
- Three `import ... from '../../api'` lines → one combined import

## Output format

For each finding, provide:
- **File and line**
- **Rule violated** (which principle)
- **Before** (current code)
- **After** (suggested fix)

Prioritize findings by impact: most lines saved first.

End with a summary: how many lines can be removed, and the top 3 highest-impact changes.
