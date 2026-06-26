const OUTAGE_FLAG_CACHE_KEY = 'sebt_outage_page_enabled'

/**
 * Reads the last-known outage flag from sessionStorage.
 * Returns null when unavailable (SSR) or when no value has been cached yet.
 */
export function readCachedOutageFlag(): boolean | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    const value = window.sessionStorage.getItem(OUTAGE_FLAG_CACHE_KEY)
    if (value === 'true') {
      return true
    }
    if (value === 'false') {
      return false
    }
    return null
  } catch {
    return null
  }
}

/** Persists the outage flag so the next navigation can gate immediately when true. */
export function writeCachedOutageFlag(enabled: boolean): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.sessionStorage.setItem(OUTAGE_FLAG_CACHE_KEY, enabled ? 'true' : 'false')
  } catch {
    // sessionStorage may be unavailable in private browsing or strict environments
  }
}

/** Clears cached outage state — exposed for tests. */
export function clearCachedOutageFlag(): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.sessionStorage.removeItem(OUTAGE_FLAG_CACHE_KEY)
  } catch {
    // ignore
  }
}
