# Missing Locale Strings Report

Generated: 2026-02-26 | CSVs: co.csv and dc.csv (latest)

Cross-referenced all fallback strings against both CSVs and generated JSON files. Every item below has been verified — no CSV row exists (even with different wording) unless noted otherwise.

---

## DC — Empty Keys (CSV rows exist, DC English column empty)

CO English and Spanish columns are populated for all of these. DC just needs English values added.

#### `en/dc/dashboard.json` — 2 empty (used by code, have fallbacks) + 12 unused placeholders

**Used by `CardStatusTimeline.tsx` (have English fallbacks, low priority):**

| Key                          | Fallback in code | CO English value | CSV verification                          |
| ---------------------------- | ---------------- | ---------------- | ----------------------------------------- |
| `cardTableStatusActive`      | "Active"         | "Active"         | dc.csv:178 — DC col 1 empty, CO has value |
| `cardTableStatusDeactivated` | "Deactivated"    | "Inactive"       | dc.csv:179 — DC col 1 empty, CO has value |

Note: CO intentionally uses "Inactive" for the deactivated status. Code fallback "Deactivated" only renders for DC.

**Not referenced by any component (future/placeholder rows — no action needed):**

| Key                                   | Context                   |
| ------------------------------------- | ------------------------- |
| `cardTableStatusInactive`             | Not used by any component |
| `cardTableStatusFrozen`               | Not used by any component |
| `cardTableStatusUndeliverable`        | Not used by any component |
| `cardTableStatusMessageRequested1`    | Not used by any component |
| `cardTableStatusMessageRequested2`    | Not used by any component |
| `cardTableStatusMessageMailed`        | Not used by any component |
| `cardTableStatusMessageActive`        | Not used by any component |
| `cardTableStatusMessageDeactivated`   | Not used by any component |
| `cardTableStatusMessageInactive`      | Not used by any component |
| `cardTableStatusMessageFrozen`        | Not used by any component |
| `cardTableStatusMessageUndeliverable` | Not used by any component |
| `cardTableActionUpdateRequest`        | Not used by any component |

#### `en/dc/editContactPreferences.json` — 1 empty

| Key                         | Context                    | CO English value                                                                    |
| --------------------------- | -------------------------- | ----------------------------------------------------------------------------------- |
| `descriptionTextPreference` | Helper text for SMS opt-in | "This phone number needs to be able to receive text messages and can't be changed." |

#### `en/dc/email.json` — 1 empty

| Key      | Context                    | CO English value             |
| -------- | -------------------------- | ---------------------------- |
| `title2` | Secondary email page title | "What's your email address?" |

#### `en/dc/idProofing.json` — 1 placeholder

| Key        | Value   | Context                                                        |
| ---------- | ------- | -------------------------------------------------------------- |
| `helperId` | `"!!!"` | Placeholder in all 4 language columns — fix or remove from CSV |

---

## CO — Empty Keys (CO column empty, DC column populated)

These CSV rows exist and have DC English values. CO needs its own values added to column 1.

#### `en/co/common.json` — 10 empty (need CO values)

| Key                   | DC English value (for reference) | Notes                                    |
| --------------------- | -------------------------------- | ---------------------------------------- |
| `programName`         | "Summer EBT"                     | CO likely same                           |
| `language`            | "Language"                       | CO likely same                           |
| `linkFaqs`            | "FAQs"                           | CO likely same                           |
| `linkContactUs`       | "Contact us"                     | CO likely same                           |
| `linkPublicNotices`   | "Public Notifications"           | CO may want different wording            |
| `linkAccessibility`   | "Accessibility"                  | CO likely same                           |
| `linkPrivacyPolicy`   | "Privacy and Security"           | CO likely same                           |
| `linkGoogleTranslate` | "Google Translate Disclaimer"    | CO likely same                           |
| `linkAbout`           | "About DC.GOV"                   | CO needs "About Colorado.gov" or similar |
| `linkTerms`           | "Terms and Conditions"           | CO likely same                           |

#### Other CO empty keys (CO column empty, DC column populated)

| File                                | Key                                     | DC English value                                                    |
| ----------------------------------- | --------------------------------------- | ------------------------------------------------------------------- |
| `en/co/common.json`                 | `copyrite`                              | "© 2026 District of Columbia" — CO needs "© 2026 State of Colorado" |
| `en/co/confirmInfo.json`            | `actionHelp`                            | "Contact us"                                                        |
| `en/co/dashboard.json`              | `applicationsTableHeadingDateSubmitted` | "Date submitted"                                                    |
| `en/co/editContactPreferences.json` | `labelPhone`                            | "What's the best phone number to text you?"                         |
| `en/co/editContactPreferences.json` | `descriptionPhone`                      | "This phone number needs to be able to receive text messages."      |

---

## Keys Missing from CSV (no row exists — need new CSV rows)

All items below are wired in code with `t('key', 'English fallback')` so they render correctly in English. They need CSV rows added for proper Spanish translation support.

Each item was cross-referenced against both CSVs and all generated JSON files to confirm no existing row covers it, even under a different key name or wording.

### Both states — accessibility labels (wired with fallbacks)

These are screen-reader-only strings. CSVs don't typically have rows for ARIA labels.

| File                     | Key used in code                          | English fallback         | Verified against CSV                          |
| ------------------------ | ----------------------------------------- | ------------------------ | --------------------------------------------- |
| `Footer.tsx`             | `common.footerNavLabel`                   | "Footer navigation"      | No footer nav label in CSV                    |
| `ActionButtons.tsx`      | `dashboard.actionNavigationNavLabel`      | "Quick actions"          | No action nav label in CSV                    |
| `CardStatusTimeline.tsx` | `dashboard.cardTableStatusAriaLabel`      | "Card status timeline"   | No card status aria label in CSV              |
| `CardStatusTimeline.tsx` | `dashboard.cardTableStatusNotComplete`    | "not complete"           | No "not complete" sr-only text in CSV         |

### Both states — dashboard content (wired with fallbacks)

| File                     | Key used in code                            | English fallback                                                        | Verified against CSV                                                                        |
| ------------------------ | ------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `CardStatusTimeline.tsx` | `dashboard.cardTableStatusUnknown`          | "Unknown"                                                               | No "Unknown" status in CSV — CSV has Active/Deactivated/Inactive/Frozen/Undeliverable only  |
| `CardStatusTimeline.tsx` | `dashboard.cardTableStatusLabelRequested`   | "Requested"                                                             | CSV has `"Requested on [MM/DD/YYYY]"` (date template) — code needs bare label; date shown separately |
| `CardStatusTimeline.tsx` | `dashboard.cardTableStatusLabelMailed`      | "Mailed"                                                                | CSV has `"Mailed on [MM/DD/YYYY]"` (date template) — code needs bare label; date shown separately    |
| `DashboardContent.tsx`   | `dashboard.pageTitle`                       | "SUN Bucks Dashboard"                                                   | No page title row in CSV — dashboard section starts at alerts                               |
| `DashboardContent.tsx`   | `dashboard.errorHeading`                    | "Error loading dashboard"                                               | No error rows in dashboard CSV section                                                       |
| `DashboardContent.tsx`   | `dashboard.errorDescription`                | "There was an error loading your dashboard. Please try again later."    | No error rows in dashboard CSV section                                                       |
| `EbtEdgeSection.tsx`     | `dashboard.alertEbtEdgeSectionHeading`      | "EBT Card Help"                                                         | Distinct from `alertEbtEdgeTitle` ("Check balance or change PIN number") — sr-only `<h2>`   |
| `IdProofingForm.tsx`     | Uses existing `common.linkContactUs`        | **FIXED** — now uses existing CSV key from common namespace             | CSV: `GLOBAL - Link Contact Us` = "Contact us"                                             |

### Both states — error pages (wired with fallbacks)

These pages were coded by us, not sourced from the state partner CSV. No CSV rows exist for any error/not-found page content.

| File                          | Keys used in code                                          | English fallbacks                                                                    |
| ----------------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `app/error.tsx`               | `common.errorSomethingWentWrong`, `errorUnexpectedBody`, `errorId`, `errorTryAgain` | "Something went wrong", "An unexpected error occurred...", "Error ID:", "Try again" |
| `app/(authenticated)/error.tsx` | `common.errorSessionExpired`, `errorSessionExpiredBody`, `errorPageBody`, `errorLogInAgain` | "Session expired", "Your session has expired...", error page body, "Log in again" |
| `app/not-found.tsx`           | `common.pageNotFound`, `pageNotFoundBody`, `returnToHome`  | "Page not found", "The page you are looking for...", "Return to home"                |

### Both states (still hardcoded — need CSV rows AND code wiring)

| File                 | Hardcoded String                                          | Suggested key                |
| -------------------- | --------------------------------------------------------- | ---------------------------- |
| `IdProofingForm.tsx` | `optionLabelNone` radio — "I don't have any of these IDs" | `idProofing.optionLabelNone` |

### DC only

| File                | Hardcoded String                            | Status                                                                      |
| ------------------- | ------------------------------------------- | --------------------------------------------------------------------------- |
| `VerifyOtpForm.tsx` | `"A new code has been sent to your email."` | Value in CSV under broken row key `"VALIDATION -"` — needs CSV key name fix |

### CO only (wired with fallbacks)

| File                    | Key used in code              | English fallback           | Verified against CSV                      |
| ----------------------- | ----------------------------- | -------------------------- | ----------------------------------------- |
| `Footer.tsx` (COFooter) | `common.transparencyOnline`   | "Transparency Online"      | No CO CSV row for footer links            |
| `Footer.tsx` (COFooter) | `common.generalNotices`       | "General Notices"          | No CO CSV row for footer links            |
| `Footer.tsx` (COFooter) | `common.copyrite`             | "© 2026 State of Colorado" | CSV has `GLOBAL - Copyrite` but CO col is empty |

### CO only (still hardcoded — need CSV rows AND code wiring)

| File              | Hardcoded String                         | Suggested key                          |
| ----------------- | ---------------------------------------- | -------------------------------------- |
| `HelpSection.tsx` | `"Help and Support"` (section heading)   | `common.helpAndSupport`                |
| `HelpSection.tsx` | `"Summer EBT Help Desk"`                 | `common.helpDeskTitle`                 |
| `HelpSection.tsx` | `"Email the Summer EBT Help Desk at..."` | `common.helpDeskBody`                  |
| `HelpSection.tsx` | `"cdhs_sebt_supportcenter@state.co.us"`  | `common.helpDeskEmail`                 |
| `HelpSection.tsx` | `"Accessibility at CDHS"`                | `common.accessibilityTitle`            |
| `HelpSection.tsx` | `"CDHS is committed to meeting..."`      | `common.accessibilityBody`             |
| `HelpSection.tsx` | `"Digital accessibility statement"`      | `common.digitalAccessibilityStatement` |

---

## Data Quality Issues

| Location                                       | Issue                                                                                            |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `es/dc/editMailingAddress.json` `optionDelete` | Typo: `"Borarr"` → `"Borrar"`                                                                    |
| `es/co/editMailingAddress.json` `optionDelete` | Same typo: `"Borarr"` → `"Borrar"`                                                               |
| `en/dc/idProofing.json` `helperId`             | Placeholder `"!!!"` (all 4 columns)                                                              |
| dc.csv row 646 / co.csv row 652               | Malformed key `"VALIDATION -"` (no key name) — orphans "A new code has been sent to your email." |
| DC `common.json` rows 49-60                    | Column-shifted values (`programName` = "Language", `language` = "Translate", etc.) — known CSV alignment bug |

---

## Summary

| Category                                    | DC  | CO  | Both |
| ------------------------------------------- | --- | --- | ---- |
| Empty keys used by code (have fallbacks)    | 2   | 15  | —    |
| Empty keys not referenced (placeholders)    | 12  | —   | —    |
| Empty keys (needs value in CSV)             | 2   | —   | —    |
| Missing CSV rows (code wired with fallback) | 1   | 3   | 23   |
| Missing CSV rows (still hardcoded)          | —   | 7   | 1    |
| Data quality issues                         | —   | —   | 5    |

**Remaining work (needs CSV/content changes):**

1. **CO common footer values** (10 empty keys) — CSV rows exist, CO column just needs filling
2. **DC card status values** (2 used keys with code fallbacks) — low priority, fallbacks render correctly
3. **Add new CSV rows** for wired-with-fallback keys (27 items) — English works, needed for Spanish
4. **Wire `HelpSection.tsx`** hardcoded strings (7 items) — needs CSV rows first, then code wiring
5. **Wire `IdProofingForm.tsx` `optionLabelNone`** — needs CSV row first, then code wiring
6. **Fix `"Borarr"` typo** and `"!!!"` placeholder in CSVs
7. **Fix broken CSV row** `"VALIDATION -"` to generate proper key name
8. **Fix DC common.json column shift** — CSV alignment issue causing wrong values in DC footer keys
