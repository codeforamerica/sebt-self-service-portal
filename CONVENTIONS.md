# Conventions: Short, Sharp, Simple

## Code Priorities (in order)

1. **It works** - baseline requirement
2. **Short** - fewest lines possible. Linear flow (start to finish), no unnecessary "hallways" of functions
3. **Sharp** - solves exactly the problem, fully, including edge cases. No "what if in the future..." generalization
4. **Simple** - if it feels complicated, stop and simplify

**Deleting code is encouraged.** Every line removed is a win.

---

## Rules

### Write linear code - avoid deep call chains

Code should read top to bottom. If understanding a component requires jumping through 4 helper functions, it's too fragmented. Flatten the logic so a reader can follow the flow in one place.

```tsx
// BAD - handler → toUiCardStatus → getStatusLabelKey → t() in 3 hops
function getStatusLabelKey(uiStatus: UiCardStatus): string {
  switch (uiStatus) {
    case 'Active': return 'cardTableStatusActive'
    default: return 'cardTableStatusInactive'
  }
}

function getStatusColor(uiStatus: UiCardStatus): string {
  switch (uiStatus) {
    case 'Active': return 'bg-success-dark text-white'
    default: return 'bg-error-dark text-white'
  }
}

export function CardStatusDisplay({ application }: Props) {
  const uiStatus = toUiCardStatus(application.cardStatus)
  const labelKey = getStatusLabelKey(uiStatus)
  const colorClass = getStatusColor(uiStatus)
  return <span className={`usa-tag ${colorClass}`}>{t(labelKey)}</span>
}

// GOOD - read top to bottom, no jumping
const STATUS_CONFIG: Record<UiCardStatus, { label: string; colorClass: string }> = {
  Active:       { label: t('cardTableStatusActive'),       colorClass: 'bg-success-dark text-white' },
  Inactive:     { label: t('cardTableStatusInactive'),     colorClass: 'bg-error-dark text-white' },
  Frozen:       { label: t('cardTableStatusFrozen'),       colorClass: 'bg-warning-dark text-white' },
  Undeliverable:{ label: t('cardTableStatusUndeliverable'),colorClass: 'bg-error-dark text-white' },
}
```

### Don't create a function if it's only called once

Inline the logic. A one-call function is just indirection.

```tsx
// BAD - function only called from one place
function hasDcCardLifecycle(application: Application): boolean {
  return application.cardRequestedAt != null
}

// then:
if (hasDcCardLifecycle(application)) { ... }

// GOOD - inline it if the intent is clear from context
if (application.cardRequestedAt != null) { ... }

// EXCEPTION - keep it if the name adds real clarity that the expression alone doesn't give
// hasDcCardLifecycle is fine here because "cardRequestedAt != null" doesn't communicate
// "DC card lifecycle" to a reader — the name buys real meaning.
```

```csharp
// BAD - method called once
private bool IsEligibleForRenewal(Application application)
{
    return application.Status == ApplicationStatus.Approved
        && application.ExpirationDate > DateTime.UtcNow;
}

// in handler:
if (IsEligibleForRenewal(application)) { ... }

// GOOD - inline it
if (application.Status == ApplicationStatus.Approved && application.ExpirationDate > DateTime.UtcNow)
{ ... }
```

### Delete unused code

Don't keep dead code, placeholder functions, or exports nobody imports.

```tsx
// BAD - CardStatusTimeline exported but only used in ChildCard
export function CardStatusTimeline(...) { ... }  // only ChildCard imports this
export function CardStatusDisplay(...) { ... }   // only ChildCard imports this

// The export keyword just adds noise if nothing outside the feature imports it.
// GOOD - keep exports for components intended to be used outside the feature.
// For internal components, drop the export.

// BAD - commented-out block
// const oldFormatDate = (date: string) => {
//   return new Date(date).toLocaleDateString()
// }

// GOOD - delete it. Git has history.
```

```csharp
// BAD - placeholder for future state
public async Task HandleColoradoWorkflow(Application application)
{
    // Colorado workflow - placeholder for future implementation
}

// GOOD - delete it. Add it when needed.
```

### Never hardcode display strings — always use i18n

Every user-facing string goes through i18next. The content pipeline is: Google Sheet → CSV → `generate-locales.js` → JSON. Don't skip it.

```tsx
// BAD - hardcoded display text
<span>Active</span>
<p>Your card has been mailed to your address on file.</p>
<Link href="/cards/request">Request a replacement card</Link>

// BAD - editing JSON locale files directly (they're auto-generated)
// src/SEBT.Portal.Web/content/locales/en/dc/dashboard.json ← DON'T TOUCH

// GOOD - use translation keys
<span>{t('cardTableStatusActive')}</span>
<p>{t('cardTableStatusMessageMailed')}</p>
<Link href="/cards/request">{t('cardTableActionRequestReplacement')}</Link>

// GOOD - provide a fallback when DC locale has empty values pending content-team updates
// i18next returns '' (not the fallback arg) when a key exists with an empty string value
const label = t('cardTableStatusActive') || 'Active'
```

### Use USWDS tokens — never hardcode colors or spacing

The design system handles contrast, theming, and accessibility. Hardcoding values bypasses all of that.

```tsx
// BAD - hardcoded hex or arbitrary Tailwind values
<span style={{ backgroundColor: '#00A398' }}>Active</span>
<div className="p-4 border-l-4 border-teal-500 bg-teal-50">

// BAD - magic numbers
<div className="mt-[12px] mb-[8px]">

// GOOD - USWDS utility classes and design tokens
<span className="bg-primary-light text-white">Active</span>
<div className="padding-2 border-left-1 border-primary-light bg-primary-lighter">

// GOOD - semantic USWDS color roles
className="bg-success-dark"   // active/positive
className="bg-error-dark"     // inactive/negative
className="bg-warning-dark"   // frozen/warning
className="bg-base-lighter"   // neutral/muted
```

### Don't create wrapper components for one-time use

If a component is only rendered in one place and extracting it doesn't add meaningful reuse or readability, inline it.

```tsx
// BAD - wrapper whose only job is passing props through
function CardStatusBadge({ status }: { status: string }) {
  return <span className="usa-tag">{status}</span>
}

// only used once:
<CardStatusBadge status={label} />

// GOOD - just inline it
<span className="usa-tag">{label}</span>

// EXCEPTION - extract when:
// 1. Used in 2+ places, OR
// 2. The extraction meaningfully reduces noise at the call site
```

### Keep Zod schemas and TypeScript types co-located

The pattern in this project is `api/schema.ts` for Zod schemas, `api/index.ts` for derived types and helper functions. Don't scatter types across files.

```tsx
// BAD - type defined in the component file
// ChildCard.tsx
type CardStatus = 'Active' | 'Mailed' | 'Requested' | ...

// BAD - duplicate type definition in multiple places
// useHouseholdData.ts
type Application = { ... }
// ChildCard.tsx
type Application = { ... }  // same shape

// GOOD - one source of truth
// api/schema.ts  → Zod schema
// api/index.ts   → export type Application = z.infer<typeof applicationSchema>
// everywhere else → import type { Application } from '../../api'
```

### Keep C# handlers in Clean Architecture layers

Don't let infrastructure concerns leak into use cases or domain logic.

```csharp
// BAD - EF Core DbContext referenced directly in a use case handler
public class GetHouseholdHandler : IRequestHandler<GetHouseholdQuery, HouseholdDto>
{
    private readonly AppDbContext _context;  // ← infrastructure leaking into use case

    public async Task<HouseholdDto> Handle(...)
    {
        return await _context.Households.FirstOrDefaultAsync(...);
    }
}

// GOOD - depend on the repository interface (defined in Core)
public class GetHouseholdHandler : IRequestHandler<GetHouseholdQuery, HouseholdDto>
{
    private readonly IHouseholdRepository _repository;

    public async Task<HouseholdDto> Handle(...)
    {
        return await _repository.GetByIdAsync(...);
    }
}
```

### Don't duplicate logic across similar branches

```tsx
// BAD - two nearly identical branches
{application.cardStatus === 'Mailed' && (
  <p>{t('cardTableStatusMessageMailed')}</p>
)}
{application.cardStatus === 'Processed' && (
  <p>{t('cardTableStatusMessageMailed')}</p>
)}

// GOOD - combine them
{(application.cardStatus === 'Mailed' || application.cardStatus === 'Processed') && (
  <p>{t('cardTableStatusMessageMailed')}</p>
)}
```

### Collapse single-line blocks

```tsx
// BAD
if (!cardStatus) {
  return null
}

// GOOD
if (!cardStatus) return null
```

```csharp
// BAD
if (application == null)
{
    throw new NotFoundException(application.Id);
}

// GOOD
if (application == null) throw new NotFoundException(id);
```

### Merge related guard clauses

```tsx
// BAD
if (!cardStatus) return null
if (cardStatus === 'Unknown') return null
if (cardStatus === 'Requested') return null

// GOOD
if (!cardStatus || cardStatus === 'Unknown' || cardStatus === 'Requested') return null
```

### Combine imports from the same module

```tsx
// BAD
import type { Application } from '../../api'
import type { CardStatus } from '../../api'
import { isReplacementEligible } from '../../api'

// GOOD
import type { Application, CardStatus } from '../../api'
import { isReplacementEligible } from '../../api'
```

### Don't write section comments for obvious code

```tsx
// BAD
// Keys map to CSV: "S2 - Portal Dashboard - Card Table - Status {Status}"
// (repeated on 5 consecutive functions that obviously all map to CSV keys)

// BAD
// Render the badge
return <span className="usa-tag">{label}</span>

// GOOD - comment only when the WHY isn't obvious from the code
// i18next returns '' (not the fallback arg) when a key exists with an empty value.
// DC locale has these blank pending content-team updates to the Google Sheet.
const label = t('cardTableStatusActive') || 'Active'
```

### Make internal functions and types private

If a function, type, or component is only used within its own file, don't export it.

```tsx
// BAD - exported but nothing outside this file imports it
export function getBorderClass(status: CardStatus): string { ... }
export type StepConfig = { borderClass: string; bgClass: string; icon: string }

// GOOD
function getBorderClass(status: CardStatus): string { ... }
type StepConfig = { borderClass: string; bgClass: string; icon: string }
```

### Write tests before implementation (TDD)

The project follows test-driven development. Write a failing test first, then write the minimum implementation to make it pass.

```tsx
// Pattern for React component tests in this project:
import { render, screen } from '@testing-library/react'
import { createMockApplication } from '../../testing'  // ← use project factory, not raw objects

// For CO-specific components (tested under DC locale by default):
import i18n from '@/lib/i18n'
import enCODashboard from '@/content/locales/en/co/dashboard.json'
beforeAll(() => { i18n.addResourceBundle('en', 'dashboard', enCODashboard, true, true) })
afterAll(() => { i18n.removeResourceBundle('en', 'dashboard') })

// Test behavior, not implementation
// BAD
expect(component.state.isLoading).toBe(false)

// GOOD
expect(screen.getByText('Active')).toBeInTheDocument()
expect(screen.queryByRole('link')).toBeNull()
```

---

## Summary

| Principle | Ask yourself |
|-----------|-------------|
| **Short** | Can I delete this line? Can I inline this function? |
| **Sharp** | Does this solve the actual problem or a hypothetical one? |
| **Simple** | Would a junior dev understand this in 30 seconds? |

**Deleting code is encouraged.** Every line removed is a win.
