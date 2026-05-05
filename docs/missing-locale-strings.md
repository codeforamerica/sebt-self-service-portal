# Missing Locale Strings Report

Generated: 2026-05-04 | CSVs: co.csv and dc.csv (latest)

Cross-referenced all fallback strings against both CSVs and generated JSON files. Every item below has been verified — no CSV row exists (even with different wording) unless noted otherwise.

---

## DC — `dashboard.json` keys with empty DC English column

Verified against `packages/design-system/content/states/dc.csv` and the rendered components in `src/SEBT.Portal.Web/src/features/household/components/{CardStatusDisplay,CardStatusTimeline}/`.

In dc.csv, the message rows (descriptions, second column) have an empty "DC English Current" column for the keys below — the SOURCE column has English copy but the locale generator only reads the per-state column. For each, code in `CardStatusDisplay.tsx` or `CardStatusTimeline.tsx` falls back to a hardcoded English string from `LABEL_FALLBACK`, `DESCRIPTION_FALLBACK`, or `REQUESTED_*_FALLBACK`.

**Used by `CardStatusDisplay.tsx` `STATUS_CONFIG` (label rendered for each UI bucket):**

| Key                            | DC col 1 | CO col 1 | Code fallback when key empty                                |
| ------------------------------ | -------- | -------- | ----------------------------------------------------------- |
| `cardTableStatusActive`        | "Active" | "Active" | `LABEL_FALLBACK.Active = "Active"`                          |
| `cardTableStatusInactive`      | "Inactive" | "Inactive" | `LABEL_FALLBACK.Inactive = "Inactive"`                  |
| `cardTableStatusFrozen`        | "Frozen" | "Frozen" | `LABEL_FALLBACK.Frozen = "Frozen"`                          |
| `cardTableStatusUndeliverable` | "Undeliverable" | "Undeliverable" | `LABEL_FALLBACK.Undeliverable = "Undeliverable"` |

**Used by `CardStatusDisplay.tsx` `DESCRIPTION_KEY` (paragraph rendered below the badge), DC col 1 empty, CO col 1 has English content:**

| Key                                   | Mapped enum values                                | Code fallback when key empty                                                                                       |
| ------------------------------------- | ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `cardTableStatusMessageActive`        | `Active`                                          | `DESCRIPTION_FALLBACK.Active` (English EBT Customer Service text)                                                  |
| `cardTableStatusMessageInactive`      | `Lost`, `Stolen`, `Damaged`                       | `DESCRIPTION_FALLBACK.Lost`/`Stolen`/`Damaged` ("This card was reported as lost, stolen, or damaged...")           |
| `cardTableStatusMessageDeactivated`   | `DeactivatedByState`, `NotActivated`              | `DESCRIPTION_FALLBACK.DeactivatedByState` and `.NotActivated` (per-status English copy)                            |
| `cardTableStatusMessageFrozen`        | `Frozen`                                          | `DESCRIPTION_FALLBACK.Frozen` ("This card is frozen. Contact customer service for help.")                          |
| `cardTableStatusMessageUndeliverable` | `Undeliverable`                                   | `DESCRIPTION_FALLBACK.Undeliverable` ("This card was returned as undeliverable...")                                |

**Used by `CardStatusTimeline.tsx` (cooldown notice after a replacement request):**

| Key                                | DC col 1                          | CO col 1 | Code fallback when key empty                                                          |
| ---------------------------------- | --------------------------------- | -------- | ------------------------------------------------------------------------------------- |
| `cardTableStatusRequested`         | "Requested on [MM/DD/YYYY]"       | (empty)  | `REQUESTED_LABEL_FALLBACK = "Requested on [MM/DD/YYYY]"`                              |
| `cardTableStatusMessageRequested1` | (empty)                           | (empty)  | `REQUESTED_MESSAGE_FALLBACK` (state-neutral English; "We've requested a new card...") |

**Truly not referenced by any component (rows can be retired from the spreadsheet):**

| Key                                | Why it's unused                                                                                |
| ---------------------------------- | ---------------------------------------------------------------------------------------------- |
| `cardTableStatusMessageRequested2` | Was for distinguishing replacement vs new-enrollee cooldown copy; never wired to a render path |
| `cardTableActionUpdateRequest`     | No component renders this key                                                                  |
| `cardTableStatusMailed`            | Was used by the old multi-step `CardStatusTimeline`; removed in DC-256                          |
| `cardTableStatusMessageMailed`     | Same — old timeline only                                                                       |
| `cardTableStatusIssued`            | Was used by the old timeline as the label for `Mailed` status; removed in DC-256                |
| `cardTableStatusDeactivated`       | Was the label for the now-removed `CardStatus.Deactivated` enum value; removed in DC-256        |

**Note on `cardTableStatusMessageDeactivated`:** Despite the value name suggesting otherwise, this key IS still used. `CardStatusDisplay.DESCRIPTION_KEY` maps both `DeactivatedByState` and `NotActivated` to it. Don't retire the row.

**Keys missing from CSV entirely (no row exists, code falls back):**

| Key                              | Suggested English                                  | Used by                                                                                                                                                                            |
| -------------------------------- | -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `cardTableStatusProcessed`       | "Processed on [MM/DD/YYYY]"                        | `CardStatusDisplay.STATUS_CONFIG.Processed`. DC's primary card status when an issue date is present. Code falls back to `LABEL_FALLBACK.Processed = "Processed on [MM/DD/YYYY]"`. |
| `cardTableStatusMessageProcessed` | "Your card has been processed and is on its way." | `CardStatusDisplay.DESCRIPTION_KEY.Processed`. Code falls back to `DESCRIPTION_FALLBACK.Processed`.                                                                              |

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

(The DC-256 `Processed` keys listed in the DC dashboard.json section above also belong here — they're new and need CSV rows added.)

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

| Category                                                              | DC  | CO  | Both |
| --------------------------------------------------------------------- | --- | --- | ---- |
| `dashboard.json` keys used by code with fallbacks (DC col 1 empty)    | 11  | 1   | —    |
| `dashboard.json` keys truly unreferenced (rows can be retired)        | 6   | —   | —    |
| `dashboard.json` keys missing from CSV entirely (need new CSV rows)   | —   | —   | 2    |
| Other empty keys (other JSON files)                                   | 3   | 15  | —    |
| Missing CSV rows (code wired with fallback)                           | 1   | 3   | 23   |
| Missing CSV rows (still hardcoded)                                    | —   | 7   | 1    |
| Data quality issues                                                   | —   | —   | 5    |

**Remaining work (needs CSV/content changes):**

1. **DC dashboard message values** (11 keys with empty DC col 1) — CSV rows exist, code falls back to English; populate DC col 1 for proper rendering
2. **`cardTableStatusProcessed` and `cardTableStatusMessageProcessed`** — new keys introduced by DC-256, need CSV rows added (both DC and CO English + Spanish)
3. **Retire 6 unreferenced rows** — `cardTableStatusMessageRequested2`, `cardTableActionUpdateRequest`, `cardTableStatusMailed`, `cardTableStatusMessageMailed`, `cardTableStatusIssued`, `cardTableStatusDeactivated`
4. **CO common footer values** (10 empty keys) — CSV rows exist, CO column just needs filling
5. **Add new CSV rows** for other wired-with-fallback keys (~25 items: ARIA labels, error pages, dashboard content) — English works, needed for Spanish
6. **Wire `HelpSection.tsx`** hardcoded strings (7 items) — needs CSV rows first, then code wiring
7. **Wire `IdProofingForm.tsx` `optionLabelNone`** — needs CSV row first, then code wiring
8. **Fix `"Borarr"` typo** and `"!!!"` placeholder in CSVs
9. **Fix broken CSV row** `"VALIDATION -"` to generate proper key name
10. **Fix DC common.json column shift** — CSV alignment issue causing wrong values in DC footer keys
