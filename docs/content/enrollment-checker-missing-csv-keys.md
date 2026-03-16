# Enrollment Checker — Missing CSV Content Keys

These keys are referenced by the enrollment checker components but are not present in either
`packages/design-system/content/states/co.csv` or `dc.csv`. They are currently served from
manually-written locale JSON files checked into git.

**Action required:** Add these rows to both state CSVs, then re-run `pnpm copy:generate` and
remove the manually-written JSON files from version control.

## Suggested CSV row format

```
{CSV Row Key}, {English (CO)}, {English (DC)}, {Español (CO)}, {Español (DC)}
```

---

## `personalInfo` namespace (S1 - Personal Information)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `personalInfo.editHeading` | `S1 - Personal Information - Edit Heading` | "Edit child information" |
| `personalInfo.firstNameLabel` | `S1 - Personal Information - Label First Name` | "First name" |
| `personalInfo.middleNameLabel` | `S1 - Personal Information - Label Middle Name` | "Middle name (optional)" |
| `personalInfo.lastNameLabel` | `S1 - Personal Information - Label Last Name` | "Last name" |
| `personalInfo.dobHint` | `S1 - Personal Information - DOB Hint` | "Format: YYYY-MM-DD" |
| `personalInfo.schoolLabel` | `S1 - Personal Information - Label School` | "School" |
| `personalInfo.schoolSelectPlaceholder` | `S1 - Personal Information - School Placeholder` | "Select a school" |

> Note: `firstNameLabel`, `middleNameLabel`, `lastNameLabel` have near-equivalents in the
> `common` namespace (`common.labelFirstName`, etc.) but the enrollment checker form uses them
> from `personalInfo` so the `personalInfo`-specific entries are needed, or the components
> should be updated to reference `common` with `{ ns: 'common' }`.

---

## `result` namespace (S1 - Result)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `result.heading` | `S1 - Result - Heading` | "Check results" |
| `result.errorHeading` | `S1 - Result - Error Heading` | "Results unavailable" |
| `result.enrolledHeading` | `S1 - Result - Enrolled Heading` | "Children enrolled in Summer EBT" |
| `result.notEnrolledHeading` | `S1 - Result - Not Enrolled Heading` | "Children not enrolled in Summer EBT" |
| `result.notEnrolledCta` | `S1 - Result - Not Enrolled CTA` | "These children may still be eligible." |
| `result.applyLink` | `S1 - Result - Apply Link` | "Apply for Summer EBT" |
| `result.status.enrolled` | `S1 - Result - Status Enrolled` | "Enrolled" |
| `result.status.notEnrolled` | `S1 - Result - Status Not Enrolled` | "Not enrolled" |
| `result.status.error` | `S1 - Result - Status Error` | "Check unavailable" |

---

## `common` namespace (GLOBAL)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `common.cancel` | `GLOBAL - Action - Cancel` | "Cancel" |
| `common.editChild` | `GLOBAL - Action - Edit` | "Edit" |
| `common.removeChild` | `GLOBAL - Action - Remove` | "Remove" |
| `common.addAnotherChild` | `GLOBAL - Action - Add Another Child` | "Add another child" |
| `common.schoolLoading` | `GLOBAL - School Loading` | "Loading schools..." |
| `common.schoolError` | `GLOBAL - School Error` | "Unable to load schools. Please try again." |

---

## `confirmInfo` namespace (S1 - Confirm Personal Information)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `confirmInfo.rateLimitError` | `S1 - Confirm Personal Information - Rate Limit Error` | "Too many requests. Please wait a moment and try again." |
| `confirmInfo.submitError` | `S1 - Confirm Personal Information - Submit Error` | "Something went wrong. Please try again." |

---

## `disclaimer` namespace (S1 - Disclaimer)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `disclaimer.body` | `S1 - Disclaimer - Body` | "The information you provide will be kept private and secure. Using this tool will not affect your potential Summer EBT benefits." |

> Note: The CSV currently has `body1`–`body4` for the disclaimer (multi-paragraph portal flow).
> The enrollment checker uses a single condensed `body`. Either add a new row or update the
> component to compose from the existing body rows.

---

## `landing` namespace (S1 - Landing Page)

| Component key | Suggested CSV row key | Current English value |
|---|---|---|
| `landing.cta` | `S1 - Landing Page - CTA` | "Continue" |

> Note: The portal landing uses `action` / `actionEspañol` for its CTA. The enrollment checker
> uses `cta` as the key name for its "Continue" button. Consider aligning with the portal's
> `action` key instead, or add this new row.

---

## Content Mismatches (fix in spreadsheet, not code)

| State | Namespace | Issue |
|---|---|---|
| CO | `landing.body` | Body text references "Do I need to apply?" but the CO `landing.action` button label is "Apply now". Either update the body text to say "Apply now" or change the button label to "Do I need to apply?" (DC already uses "Do I need to apply?" for both). |

---

## Summary

- **20 keys** need to be added to both `co.csv` and `dc.csv`
- **6 component key mismatches** have already been fixed in code (components now reference the
  CSV-generated key names: `title`, `labelBirthdate`, `actionAdd`)
- After the content team adds these rows and `pnpm copy:generate` is run, the manually-written
  locale JSON files should be removed from git and added to `.gitignore`
