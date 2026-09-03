# Add or change a key

## Change the text of a key that exists

1. Ask the content team to change the text in the Google Sheet.
2. Export the Sheet as CSV.
3. Replace `packages/design-system/content/states/{state}.csv` with the export.
4. Regenerate the locale files:

   ```bash
   cd apps/portal/src/SEBT.Portal.Web && pnpm copy:generate
   ```

5. Commit the CSV. Do not commit the generated JSON files.

No code change is necessary. The key name did not change, so each component that uses it gets the new text.

## Add a new key

### 1. Find the namespace

Each CSV row has a name in this format:

```text
"{Section} - {Page} - {Key}"
```

The page name selects the namespace. These are the common pages:

| Page in the CSV | Namespace | Call in code |
| --- | --- | --- |
| Portal Dashboard | `dashboard` | `useTranslation('dashboard')` |
| Landing Page | `landing` | `useTranslation('landing')` |
| Personal Information | `personalInfo` | `useTranslation('personalInfo')` |
| Confirm Personal Information | `confirmInfo` | `useTranslation('confirmInfo')` |
| Result | `result` | `useTranslation('result')` |
| GLOBAL or All | `common` | `useTranslation('common')` |

DC has 22 namespaces in total. List them with this command:

```bash
ls apps/portal/src/SEBT.Portal.Web/content/locales/en/dc/
```

### 2. Derive the key name

Take the part after the page name. Convert it to camel case. Remove the spaces and the hyphens.

```text
"S2 - Portal Dashboard - Card Table - Status Active"
   Section: S2
   Page:    Portal Dashboard   -> namespace: dashboard
   Key:     Card Table Status Active -> key: cardTableStatusActive
```

### 3. Add the row to both CSV files

`dc.csv` and `co.csv` must both hold the row. If one state has no wording yet, leave that value empty. The row
itself must exist in both files.

```csv
"S2 - Portal Dashboard - Card Table - Status Active","Active","Activo"
```

> [!IMPORTANT]
> Add the row to the Google Sheet as well. The next export overwrites the CSV. A row that exists only in the CSV
> disappears at that point.

### 4. Regenerate

```bash
cd apps/portal/src/SEBT.Portal.Web && pnpm copy:generate
```

Confirm that the key reached the JSON file:

```bash
grep cardTableStatusActive content/locales/en/dc/dashboard.json
```

### 5. Use the key in a component

```tsx
const { t } = useTranslation('dashboard')
return <span>{t('cardTableStatusActive')}</span>
```

### 6. Check the other app

The Enrollment Checker takes the sections `S1`, `GLOBAL`, `S10`, `S9`, `DEV`, and `S11` only. Does the Enrollment
Checker need your string? If the section of your row is not in that list, ask the content team to move the row.

## Rules

1. Put every user-facing string in the pipeline. Never write display text in a component.
2. Add each new row to both state CSV files.
3. Commit the CSV files. Never commit a file from `content/locales/`.
4. Report an absent key as a content gap. Do not add the key to a JSON file to unblock yourself.
5. Regenerate after each CSV change, then confirm the key in the JSON output.
