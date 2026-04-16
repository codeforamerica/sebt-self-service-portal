import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  buildAuthorizationUrl,
  clearPkceStorage,
  getPkceFromStorage,
  PKCE_STORAGE_MAX_AGE_MS,
  savePkceForCallback
} from './oidc-pkce'

describe('oidc-pkce', () => {
  describe('buildAuthorizationUrl', () => {
    it('builds URL with required OIDC and PKCE query params', () => {
      const config = {
        authorizationEndpoint: 'https://auth.example.com/authorize',
        clientId: 'my-client-id',
        redirectUri: 'https://app.example.com/callback'
      }
      const codeChallenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM'
      const state = 'abc123state'

      const url = buildAuthorizationUrl(config, codeChallenge, state)

      expect(url).toContain('https://auth.example.com/authorize?')
      const parsed = new URL(url)
      expect(parsed.searchParams.get('response_type')).toBe('code')
      expect(parsed.searchParams.get('client_id')).toBe('my-client-id')
      expect(parsed.searchParams.get('redirect_uri')).toBe('https://app.example.com/callback')
      expect(parsed.searchParams.get('scope')).toBe('openid email profile phone')
      expect(parsed.searchParams.get('state')).toBe(state)
      expect(parsed.searchParams.get('code_challenge')).toBe(codeChallenge)
      expect(parsed.searchParams.get('code_challenge_method')).toBe('S256')
      expect(parsed.searchParams.get('prompt')).toBe('login')
      expect(parsed.searchParams.get('max_age')).toBe('0')
    })

    it('sets language query param from explicit argument', () => {
      const config = {
        authorizationEndpoint: 'https://auth.example.com/authorize',
        clientId: 'my-client-id',
        redirectUri: 'https://app.example.com/callback'
      }
      const url = buildAuthorizationUrl(config, 'challenge', 'state', 'es')
      const parsed = new URL(url)
      expect(parsed.searchParams.get('language')).toBe('es')
    })

    it('defaults language to en when not provided', () => {
      const config = {
        authorizationEndpoint: 'https://auth.example.com/authorize',
        clientId: 'my-client-id',
        redirectUri: 'https://app.example.com/callback'
      }
      const url = buildAuthorizationUrl(config, 'challenge', 'state')
      const parsed = new URL(url)
      expect(parsed.searchParams.get('language')).toBe('en')
    })
  })

  // generateCodeVerifier, generateCodeChallenge, generateState are now server-side
  // and tested in PkceHelperTests.cs. Client-side tests cover storage + URL building only.

  describe('savePkceForCallback / getPkceFromStorage', () => {
    beforeEach(() => {
      sessionStorage.clear()
      vi.useRealTimers()
    })

    afterEach(() => {
      sessionStorage.clear()
      vi.useRealTimers()
    })

    it('round-trips state and metadata while fresh', () => {
      const before = Date.now()
      savePkceForCallback('st', { redirectUri: 'https://app/cb', clientId: 'cid' })
      const got = getPkceFromStorage()
      expect(got).not.toBeNull()
      expect(got!.state).toBe('st')
      expect(got!.storedAtMs).toBeGreaterThanOrEqual(before)
      expect(got!.storedAtMs).toBeLessThanOrEqual(Date.now())
    })

    it('returns null and clears storage when older than max age', () => {
      vi.useFakeTimers()
      const t0 = Date.now()
      vi.setSystemTime(t0)
      savePkceForCallback('st', { redirectUri: 'https://app/cb', clientId: 'cid' })
      vi.setSystemTime(t0 + PKCE_STORAGE_MAX_AGE_MS + 1)
      expect(getPkceFromStorage()).toBeNull()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })

    it('returns null and clears storage when storedAtMs is missing (legacy blob)', () => {
      sessionStorage.setItem(
        'oidc_co_pkce',
        JSON.stringify({
          state: 's',
          redirect_uri: 'https://r',
          client_id: 'c'
        })
      )
      expect(getPkceFromStorage()).toBeNull()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })

    it('clearPkceStorage removes the key', () => {
      savePkceForCallback('st', { redirectUri: 'https://app/cb', clientId: 'cid' })
      clearPkceStorage()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })
  })
})
