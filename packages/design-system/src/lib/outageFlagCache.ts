export interface OutageFlagCache {
  /**
   * The last-known outage flag from sessionStorage.
   * Null when unavailable (SSR) or when no value has been cached yet.
   */
  read: () => boolean | null
  /** Persists the outage flag so the next navigation can gate immediately when true. */
  write: (enabled: boolean) => void
  /** Clears cached outage state — exposed for tests. */
  clear: () => void
}

/**
 * A sessionStorage-backed cache of the last-known outage flag, scoped to one storage key.
 *
 * The portal and the enrollment checker each cache their own surface's flag, so they must not share
 * a key: an outage on one surface says nothing about the other. Everything else about the cache is
 * the same, hence the factory.
 *
 * Every access is guarded — sessionStorage throws in private browsing and in some strict
 * environments, and the guard is never the interesting failure. A miss just means the guard falls
 * back to rendering while the first fetch resolves.
 */
export function createOutageFlagCache(storageKey: string): OutageFlagCache {
  return {
    read() {
      if (typeof window === 'undefined') {
        return null
      }

      try {
        const value = window.sessionStorage.getItem(storageKey)
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
    },

    write(enabled: boolean) {
      if (typeof window === 'undefined') {
        return
      }

      try {
        window.sessionStorage.setItem(storageKey, enabled ? 'true' : 'false')
      } catch {
        // sessionStorage may be unavailable in private browsing or strict environments
      }
    },

    clear() {
      if (typeof window === 'undefined') {
        return
      }

      try {
        window.sessionStorage.removeItem(storageKey)
      } catch {
        // ignore
      }
    }
  }
}
