---
description: Repair a translation key that renders blank or shows its raw name.
keywords: i18n missing key blank empty raw untranslated debug locale namespace generator
---

# Troubleshooting content

## The key shows no text

| Symptom | Cause | Correction |
| --- | --- | --- |
| The page shows the raw key name | The key is absent from the namespace. | Add the row to both CSV files, then regenerate. |
| The page shows nothing at all | The row exists, but the value for that language is empty. | Ask the content team for the wording. |
| The text is correct for one state only | The row is in one CSV file only. | Add the row to the other CSV file. |
| The text is correct in the Portal but absent in the Enrollment Checker | The section of the row is not in the filter list. | Move the row to `S1`, `GLOBAL`, `S10`, `S9`, `DEV`, or `S11`. |
| Your edit disappeared | You edited a file under `content/locales/`. | Edit the CSV instead. The generator overwrites the JSON. |

## Checks in sequence

1. Confirm that the row exists in the CSV for your state:

   ```bash
   grep "Card Table - Status Active" packages/design-system/content/states/dc.csv
   ```

2. Regenerate the locale files:

   ```bash
   cd apps/portal/src/SEBT.Portal.Web && pnpm copy:generate
   ```

3. Confirm that the key reached the JSON output:

   ```bash
   grep cardTableStatusActive apps/portal/src/SEBT.Portal.Web/content/locales/en/dc/dashboard.json
   ```

4. Confirm that the component asks for the correct namespace. The page name in the CSV selects the namespace.

5. Confirm the key name. The generator converts the text after the page name to camel case.

## The generator does not run

The generator runs before `dev`, `build`, and `test`. Each app declares it in `predev`, `prebuild`, and `pretest`.

Run it alone to see its output:

```bash
cd apps/portal/src/SEBT.Portal.Web && pnpm copy:generate
```

The command runs 2 generators. `generate-locales.js` writes the JSON files and the TypeScript resource file.
`generate-backend-email.js` writes the email templates for the backend.

## The generator arguments

| Argument | Purpose |
| --- | --- |
| `--csv-dir` | The directory that holds the state CSV files. |
| `--out-dir` | The destination for the JSON files. |
| `--ts-out` | The destination for the generated TypeScript resource file. |
| `--app` | The app to generate for: `portal` or `enrollment`. |
| `--sections` | The sections to include. The Enrollment Checker uses this filter. |

## Email text is wrong

The same command generates the email templates of the backend. They go to
`apps/portal/src/SEBT.Portal.Infrastructure/Templates/Email/`. The file `EmailContent.dc.json` holds the text, and
`OtpEmail.html` holds the markup.

Change email wording in the Sheet, the same as any other string. Then regenerate and restart the API. The API reads
these templates at start-up.

## Related

- [Add or change a key](add-a-key.md) gives the full procedure.
- [ADR 0009](../../adr/0009-locale-section-filtering.md) explains the section filter.
- [ADR 0006](../../adr/0006-i18n-implementation.md) records the choice of i18next.
- [ADR 0010](../../adr/0010-rich-text-rendering.md) covers strings that hold markup.
