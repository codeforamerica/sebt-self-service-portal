// Duplicated from src/SEBT.Portal.Web/src/lib/applyHref.ts. Portal.Web and
// EnrollmentChecker.Web are separate pnpm workspace members and can't import
// each other's src/lib internals; the proper home for this is
// packages/design-system/src/lib/ so both apps consume one copy. Consolidating
// is a pure refactor (move file, update import sites, relocate the existing
// test, swap vi.mock targets) and should happen in its own change.

// The apply destination comes from NEXT_PUBLIC_APPLICATION_URL (declared in
// env.ts, documented in .env.local.example) so it can change via config without
// a code change — not from the CSV-driven translation, whose source-of-truth
// Google Sheet still points at the legacy `/SEBT/s/?language=en_US` URL. Read
// process.env directly (like getState) to keep this a small, stubbable helper.
//
// The var is optional: with applications closed (DC-701) a deployment may have
// no application destination at all. Null means "no link" — callers hide their
// apply link blocks rather than render a dead URL.

import { getState } from '@sebt/design-system/src/lib/state'

// Map i18next locale codes to the language param PEAK expects on its URL.
// Unknown locales fall back to en_US.
const PEAK_LANG_BY_LOCALE: Record<string, string> = {
  en: 'en_US',
  es: 'es'
}

export function getApplyHref(locale: string): string | null {
  const base = process.env.NEXT_PUBLIC_APPLICATION_URL
  if (!base) {
    return null
  }

  const url = new URL(base)

  // language and redirectFromEC are PEAK's contract, and PEAK is Colorado's
  // benefits portal. Other states point at their own destination, which would
  // not understand these and could be sent somewhere unintended by them.
  if (getState() === 'co') {
    // Overwrite any language baked into the configured URL so the destination
    // matches the language the visitor is currently viewing the checker in.
    // eslint-disable-next-line security/detect-object-injection -- locale indexes a static literal map
    url.searchParams.set('language', PEAK_LANG_BY_LOCALE[locale] ?? 'en_US')
    // CO CBMS / Deloitte read this flag on the PEAK referrer to count clicks
    // that originate in the Enrollment Checker.
    url.searchParams.set('redirectFromEC', 'Y')
  }

  return url.toString()
}
