import { getState } from '@sebt/design-system'

// Lives in code (not via the CSV-driven `applyOnlineLink` translation) because
// the source-of-truth Google Sheet still points at the legacy
// `/SEBT/s/?language=en_US` URL.

const APPLY_HREF_BY_STATE: Record<string, string> = {
  co: 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US',
  dc: '/apply'
}

export function getApplyHref(): string {
  return APPLY_HREF_BY_STATE[getState()] ?? '/apply'
}
