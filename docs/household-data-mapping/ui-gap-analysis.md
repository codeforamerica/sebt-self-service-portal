# UI Gap Analysis: Cases vs Applications Separation

After the backend data mapping fix, the API now returns:
- `summerEbtCases`: Children actively receiving benefits (populated by BOTH CO and DC)
- `applications`: Only submitted applications (not auto-eligible children)

Previously, DC only populated `applications` and CO populated both. Now both states populate both collections correctly.

## Gaps to Address

### 1. DashboardContent child count (HIGH)

**File:** `src/features/household/components/DashboardContent/DashboardContent.tsx:32`

**Issue:** Counts children from `data.applications` only. After the fix, auto-eligible children are no longer in `applications` — they're in `summerEbtCases`.

**Fix:** Count from `summerEbtCases` (enrolled children) instead of or in addition to `applications`.

### 2. HouseholdSummary status derivation (HIGH)

**File:** `src/features/household/components/HouseholdSummary/HouseholdSummary.tsx:25`

**Issue:** Derives overall status from `data.applications.map(app => app.applicationStatus)`. A household with only auto-eligible children will have zero applications, making this empty.

**Fix:** Derive status from cases (always approved/enrolled if present) combined with application statuses.

### 3. DashboardContent empty state check (OK — no change needed)

**File:** `src/features/household/components/DashboardContent/DashboardContent.tsx:73`

**Current:** `data.summerEbtCases.length === 0 && data.applications.length === 0`

**Status:** Already correct — checks both collections.

### 4. EnrolledChildren component (OK — improved)

**File:** `src/features/household/components/EnrolledChildren/EnrolledChildren.tsx:46`

**Status:** Already reads from `summerEbtCases`. Will now work correctly for both CO and DC (DC previously had this empty). The hardcoded `applicationStatus: 'Approved'` on line 56 is semantically correct since all items in `summerEbtCases` are enrolled children.

### 5. ApplicationsSection component (MEDIUM)

**File:** `src/features/household/components/ApplicationsSection/ApplicationsSection.tsx`

**Status:** Reads from `applications`. After the fix, will only show actual submitted applications — this is correct behavior.

**Issue:** Card status display (if any) on the application card may need review — card data on Application is populated for backward compat but semantically belongs on the Case.

### 6. ActionButtons issuance type (MEDIUM)

**File:** `src/features/household/components/DashboardContent/DashboardContent.tsx:87`

**Issue:** Uses `data.benefitIssuanceType` (household-level). Now that issuance type varies per case (e.g., one child on SNAP co-loaded card, another on dedicated SEBT card), this household-level value may not reflect all cases.

**Fix:** Consider showing per-case actions or deriving from the primary case's issuance type. For CO this is always SummerEbt; for DC it can vary.

### 7. New API fields not consumed (LOW)

The API now returns new fields that the UI Zod schemas don't parse:
- `SummerEbtCase.issuanceType` — per-case issuance type
- `SummerEbtCase.eligibilitySource` — how the child became eligible
- `Child.status` — per-child status within an application (replaces the removed `caseNumber`)
- `Application.applicationDate` — when the application was submitted

These are silently dropped by Zod's default behavior (unknown keys are stripped). Add them to the schemas in `src/features/household/api/schema.ts` when the UI is updated.

### 8. Child.caseNumber removed from API (LOW)

**Issue:** `ChildResponse` no longer has `caseNumber` (replaced by `status`). The UI `ChildSchema` has `caseNumber: z.string().nullable().optional()` which will now always be undefined.

**Fix:** Replace `caseNumber` with `status` in the Zod schema when updating the UI.
