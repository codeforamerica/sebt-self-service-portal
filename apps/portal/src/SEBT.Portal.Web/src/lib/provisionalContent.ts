/**
 * Provisional English copy for content rows that do not yet exist in the source spreadsheet.
 *
 * Injected into the `en` bundle only, after the generated resources load. Spanish and Amharic
 * resolve through `fallbackLng: 'en'` and render this English, which is exactly what the
 * per-call-site `t(key, 'English')` fallbacks used to do. The bundle is added with
 * `overwrite: false`, so real content wins the moment its row lands and the entry here goes
 * inert on its own.
 *
 * This is a temporary measure. Canonical copy belongs in the content sheet, not in the app.
 *
 * Two rules keep it from rotting:
 *
 *  1. Allowlist only. Never inject a key whose absence carries meaning. `!N/A!` in a state's CSV
 *     is a deliberate omission, and `applicationsTableHeadingNumber` is read through
 *     `i18n.exists()` as a per-state feature gate — injecting it would make DC start rendering a
 *     case number on the applications card.
 *
 *  2. Delete an entry once every state authors its row. `statusLabelDebt.test.ts` fails until you
 *     do, and also fails if a key here is not a dashboard status label.
 */
export const PROVISIONAL_EN = {
  dashboard: {
    profileTableStatusApplicationApproved: 'Application approved',
    profileTableStatusCancelled: 'Application cancelled',
    profileTableStatusUnknown: 'Status unavailable',
    applicationsTableStatusUnderReview: 'Under review',
    applicationsTableStatusCancelled: 'Cancelled',
    applicationsTableStatusUnknown: 'Status unavailable'
  }
} satisfies Record<string, Record<string, string>>
