---
name: i18n
description: Guide for adding, finding, or debugging i18n keys in the SEBT portal. Use when adding new user-facing text, looking up an existing key, or diagnosing a missing/empty translation.
argument-hint: "[key name or description of text]"
---

## Pipeline (read before touching anything)

```
Google Sheet → download CSV → content/states/{state}.csv → pnpm copy:generate → content/locales/en/{state}/{namespace}.json
```

**The JSON files are auto-generated. Never edit them directly.** They are overwritten every time `pnpm copy:generate` runs (which happens automatically before `pnpm dev`, `pnpm build`, and `pnpm test`).

To regenerate manually:
```bash
cd src/SEBT.Portal.Web && pnpm copy:generate
```

---

## Adding a new key

### 1. Identify the namespace

CSV rows use the format `"{Section} - {Page} - {Key}"`. The page name determines the namespace:

| Page in CSV | Namespace | `useTranslation(...)` |
|---|---|---|
| Portal Dashboard | `dashboard` | `useTranslation('dashboard')` |
| Landing Page | `landing` | `useTranslation('landing')` |
| Personal Information | `personalInfo` | `useTranslation('personalInfo')` |
| Confirm Personal Information | `confirmInfo` | `useTranslation('confirmInfo')` |
| Result | `result` | `useTranslation('result')` |
| GLOBAL / All | `common` | `useTranslation('common')` |

### 2. Derive the key name

The key is the part after the page name, camelCased, with spaces and hyphens removed.

```
"S2 - Portal Dashboard - Card Table - Status Active"
  └─ Section: S2  └─ Page: Portal Dashboard  └─ Key: Card Table Status Active
  → namespace: dashboard
  → key: cardTableStatusActive
```

### 3. Add the row to BOTH state CSVs

`content/states/dc.csv` and `content/states/co.csv` must both have the row. If the value isn't ready for a state yet, leave it blank — but the row must exist in both files.

```csv
"S2 - Portal Dashboard - Card Table - Status Active","Active","Activo"
```

**Do not add the key directly to the JSON files.** Ask the content team to update the Google Sheet, then re-download the CSV.

---

## Using a key in code

```tsx
const { t } = useTranslation('dashboard')

// Basic
<span>{t('cardTableStatusActive')}</span>

// With interpolation (replace placeholder in the string)
t('cardTableLastFourDigits').replace('[9999]', last4DigitsOfCard)
```

---

## The empty-string fallback trap

i18next returns `''` (not the fallback argument) when a key **exists** with an empty string value. DC locale has many keys pending content-team updates that are blank in the Google Sheet.

```tsx
// BAD — fallback is ignored when the key exists with ''
t('cardTableStatusActive', 'Active')  // returns '' not 'Active'

// GOOD
t('cardTableStatusActive') || 'Active'
```

Use the `|| fallback` pattern whenever a key might be blank pending a content update. Add a comment explaining why:

```tsx
// i18next returns '' (not the fallback arg) when a key exists with an empty value.
// DC locale has this key blank pending content-team updates to the Google Sheet.
const label = t('cardTableStatusActive') || 'Active'
```

---

## Testing CO-specific components

Tests run under the DC locale by default. If a component uses CO-only keys (which are empty in DC), add the CO bundle before the suite:

```tsx
import i18n from '@/lib/i18n'
import enCODashboard from '@/content/locales/en/co/dashboard.json'

beforeAll(() => { i18n.addResourceBundle('en', 'dashboard', enCODashboard, true, true) })
afterAll(() => { i18n.removeResourceBundle('en', 'dashboard') })
```

---

## Debugging a missing or empty translation

1. **Key returns the key string** (e.g., renders `"cardTableStatusActive"`) → key doesn't exist in the JSON. Check the CSV row exists and re-run `pnpm copy:generate`.
2. **Key returns `''`** → key exists in the CSV but has no value for this state. Use `|| fallback` or ask the content team to fill in the Google Sheet.
3. **Wrong namespace** → `t('key')` in one namespace won't find keys from another. Check the page-to-namespace table above.
4. **CO key empty in tests** → tests default to DC locale. Add the CO bundle in `beforeAll` (see Testing section above).
