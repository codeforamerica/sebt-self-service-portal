# Enrollment Checker: Figma vs Implementation Comparison

Figma source: [CO Enrollment Checker - Self-service Portal (WORKING)](https://www.figma.com/design/32kDQ73MSbUNdAsVi8fQzF/CO-11-3-25-%F0%9F%9F%A2-Enrollment-Checker---Self-service-Portal--WORKING-?node-id=2001-4310&p=f&m=dev)

Comparison date: 2026-03-13

## Screen 1: Landing Page (`/`)

| Aspect | Figma Design | App (Current) | Status |
|--------|-------------|---------------|--------|
| **Header logo** | CDHS logo with "COLORADO Department of Human Services" | Broken image (`logoAlt` alt text) | BUG - missing SVG asset |
| **Translate button** | Styled button with icon + "Translate English, Espanol" | Text links "English, Espanol (Spanish)" | MISMATCH |
| **SUMMER EBT logo** | Large "SUMMER EBT" branded logo between header and heading | Missing entirely | MISSING |
| **Heading** | "Get $120 in summer food benefits for your child" | Same | OK |
| **Body text** | Paragraphs with **bold** rendering ("one $120 payment" bolded) | Raw markdown `**one $120 payment**` shown as literal asterisks | BUG - no markdown rendering |
| **Primary CTA** | "Apply now" (dark button) | "Continue" | DIFFERENT - uses `cta` key ("Continue") instead of `action` key ("Apply now") |
| **Spanish CTA** | "Aplica ahora" (outline button) | Missing | MISSING |
| **Accordion FAQ** | "How do I know if my child can get Summer EBT?" expandable section with eligibility details | Missing entirely | MISSING |
| **Footer** | Full footer with Help Desk + Accessibility sections | Footer present, matches roughly | OK (minor) |
| **Footer - copyright** | "© 2026 State of Colorado" | Missing "© 2026 State of Colorado" prefix | MINOR |
| **Footer - seal** | Colorado Official State Web Portal seal image | Broken image | BUG - missing asset |

## Screen 2: Disclaimer (`/disclaimer`)

| Aspect | Figma Design | App (Current) | Status |
|--------|-------------|---------------|--------|
| **Heading** | "What to know before we begin" | Same | OK |
| **Body paragraph 1** | **"First, we'll check if your child is already enrolled."** (bold lead) + "If they are, you'll be able to confirm where and when your card will be mailed to you. If they aren't, we'll share the application form with you to fill out." | Missing - only shows generic `body` key ("The information you provide will be kept private and secure. Using this tool will not affect your potential Summer EBT benefits.") | MISSING - not using `body1`-`body4` keys |
| **Body paragraph 2** | **"The information you provide will be kept private and secure."** (bold lead) + "Using this tool will not affect your potential Summer EBT benefits or other benefits you get." | Collapsed into single short paragraph | MISSING |
| **Button layout** | "Back" (outline) + "Continue" (filled) | Same layout | OK |

## Screen 3: Personal Information / Child Form (`/check`)

| Aspect | Figma Design | App (Current) | Status |
|--------|-------------|---------------|--------|
| **Form card graphic** | Clipboard icon graphic above heading | Missing | MISSING |
| **Heading** | "Check if your child is already enrolled in Summer EBT" | "Are you sure you want to delete this address?" | BUG - wrong i18n key value (see Root Causes below) |
| **Description text** | "Please provide your student's information in the required fields below. This tool will check if your child is automatically enrolled to receive benefits." + "Asterisks (*) indicate a required field." | Missing entirely | MISSING |
| **First name hint** | "Legally as it appears on official documents, e.g. birth certificate" below label | No hint text | MISSING |
| **Middle name label** | "Middle name" with hint "Optional. If they have one." | "Middle name (optional)" | MINOR - different format but equivalent meaning |
| **Birthdate field** | Three separate fields: Month (dropdown), Day (text input), Year (text input) | Single text input with "YYYY-MM-DD" format | MISMATCH - completely different UX |
| **Back button label** | "Back" | "Cancel" | MISMATCH - button label |

## Screen 4: Review / Confirm Personal Information (`/review`)

| Aspect | Figma Design | App (Current) | Status |
|--------|-------------|---------------|--------|
| **Form card graphic** | Clipboard icon graphic | Missing | MISSING |
| **Heading** | "Here's the information we have so far" | "Check the address" | BUG - wrong i18n key value (see Root Causes below) |
| **Description** | "Review your child(ren)'s information below. You may add another child to see if they need to apply, too. Tap 'Submit' if you've finished adding children." | Missing | MISSING |
| **Child card - Name label** | "Name" label above "[First name] [M.] [Last name]" | Bold "Jane Doe" inline with date | MISMATCH |
| **Child card - Birthdate label** | "Birthdate" label above "[Month], [Day] [Year]" | "— 2015-04-12" (raw ISO date) | MISMATCH - no label, raw date format |
| **Child card - Edit link** | "Update this child's information" as a link | "Edit" and "Remove" as separate buttons/links | MISMATCH |
| **Child card separator** | Horizontal rule between children | None | MISSING |
| **Button layout** | "Back" (outline) + "Submit" (filled) as a row, "Add another child" as a link below | "Add another child" (outline button) + "Submit" (filled button) stacked | MISMATCH |

## Screen 5: Results (not reachable without backend)

Could not navigate to results screens since they require a real API response. Based on Figma, the results screens have several variants:

- **Streamlined Enrolled** (node `8025:28159`): Check icon, "Here's the information we found", colored card listing enrolled children, link to Summer EBT Portal
- **No Results** (node `8025:28170`): Clipboard icon, "Here's the information we found", card saying "We don't have enough information", "Continue your application" CTA
- **Apply for SEBT** (node `8025:28199`): Clipboard icon, "Here's the information we found", card with children needing to apply, application CTA + deadline info
- **Mixed Results** (node `8025:28212`): Both enrolled and not-enrolled children shown in separate cards
- **Error** (node `10117:78454`): Error state with retry options

These should be compared once the backend is connected or API responses are mocked.

---

## Root Causes

### 1. i18n namespace collision (CRITICAL)

The enrollment checker's `personalInfo` and `confirmInfo` locale JSON files contain values from the **main portal** CSV sections, not the S1 (enrollment checker) sections. This causes:

- `personalInfo.title` = "Are you sure you want to delete this address?" (portal's address deletion flow)
- `confirmInfo.title` = "Check the address" (portal's address confirmation flow)

The S1 section **does** have correct values in the CSV:
- `S1 - Personal Information - Title` = "Check if your child is already enrolled in Summer EBT"
- `S1 - Confirm Personal Information - Title` = "Here's the information we have so far"

**Root cause:** This is a bug in `generate-locales.js`, not a CSV structure issue.

In `packages/design-system/content/scripts/generate-locales.js`:
- **Line 96:** Both `S1` (enrollment checker) and `S3` (portal profile management) map to `'personalInfo'` in the `sectionToNamespace` config. Same collision exists for `confirmInfo`.
- **Lines 334-341:** The collision resolution uses "last one wins" logic — if a later row has a non-empty value, it overwrites.
- **CSV ordering:** S1 rows (e.g., row 42: `S1 - Personal Information - Title`) appear before S3 rows (e.g., row 269: `S3 - Confirm Delete Address - Title`), so S3's portal-specific values overwrite S1's enrollment checker values.

**Fix options:**
- A) Add an `--app` or `--section` filter to `generate-locales.js` so the enrollment checker only processes S1 rows
- B) Namespace S1 keys differently (e.g., `enrollmentPersonalInfo`) to avoid collision
- C) Change the collision resolution to warn/error instead of silently overwriting

### 2. Missing markdown/rich text rendering

The landing page body text contains markdown-style bold syntax (`**one $120 payment**`) but the component renders it as raw text via `{t('body')}`. Needs either:
- A markdown renderer (e.g., `react-markdown`)
- i18next's `<Trans>` component with `<strong>` interpolation
- Pre-processing the content strings

### 3. Missing structural elements

Several Figma design elements were never implemented:
- **SUMMER EBT logo** on landing page (between header and heading)
- **Form card graphic** (clipboard icon) on child form and review pages
- **Expandable accordion FAQ** on landing page ("How do I know if my child can get Summer EBT?")
- **Structured body paragraphs** on disclaimer (uses `body1`-`body4` keys, not generic `body`)
- **"Asterisks indicate required fields"** hint text on child form
- **Field hints** (e.g., "Legally as it appears on official documents" under first/last name)
- **Structured child review cards** with labeled Name/Birthdate rows and horizontal separators
- **Description paragraph** on review page

### 4. Missing image assets

CO state images return 404 errors:
- `public/images/states/co/logo.svg` (CDHS header logo)
- `public/images/states/co/seal.svg` (footer state seal)
- `public/images/states/co/icons/translate_Rounded.svg` (translate button icon)
- SUMMER EBT logo asset (not referenced yet)
- Form card graphic / clipboard icon (not referenced yet)

### 5. Birthdate field UX mismatch

The Figma design shows a three-part birthdate input (Month dropdown + Day text + Year text), but the implementation uses a single text input with `YYYY-MM-DD` format. This is a significant UX difference — the three-part input is more accessible and aligns with USWDS `memorable date` pattern.
