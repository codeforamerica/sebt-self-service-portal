import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  buildAuthorizationUrl,
  clearPkceStorage,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  getPkceFromStorage,
  PKCE_STORAGE_MAX_AGE_MS,
  savePkceForCallback
} from './oidc-pkce'

describe('oidc-pkce', () => {
  describe('buildAuthorizationUrl', () => {
    it('builds URL with required OIDC and PKCE query params', () => {
      const config = {
        authorizationEndpoint: 'https://auth.example.com/authorize',
        tokenEndpoint: 'https://auth.example.com/token',
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

    it('maps config.languageParam to language query param', () => {
      const config = {
        authorizationEndpoint: 'https://auth.example.com/authorize',
        tokenEndpoint: 'https://auth.example.com/token',
        clientId: 'my-client-id',
        redirectUri: 'https://app.example.com/callback',
        languageParam: 'en'
      }
      const url = buildAuthorizationUrl(config, 'challenge', 'state')
      const parsed = new URL(url)
      expect(parsed.searchParams.get('language')).toBe('en')
    })
  })

  describe('generateCodeVerifier', () => {
    it('returns a base64url string of expected length', () => {
      const verifier = generateCodeVerifier()
      expect(verifier).toMatch(/^[A-Za-z0-9_-]+$/)
      expect(verifier).not.toContain('+')
      expect(verifier).not.toContain('/')
      expect(verifier).not.toContain('=')
      expect(verifier.length).toBeGreaterThanOrEqual(40)
      expect(verifier.length).toBeLessThanOrEqual(44)
    })
  })

  describe('generateState', () => {
    it('returns a base64url string', () => {
      const state = generateState()
      expect(state).toMatch(/^[A-Za-z0-9_-]+$/)
      expect(state).not.toContain('+')
      expect(state).not.toContain('/')
      expect(state).not.toContain('=')
    })
  })

  describe('generateCodeChallenge', () => {
    it('produces S256 challenge from verifier', async () => {
      // RFC 7636; see here for the example: https://datatracker.ietf.org/doc/html/rfc7636#appendix-B
      const verifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk'
      const expectedChallenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM'

      const challenge = await generateCodeChallenge(verifier)

      expect(challenge).toBe(expectedChallenge)
    })

    it('returns base64url (no +, /, or =)', async () => {
      const verifier = generateCodeVerifier()
      const challenge = await generateCodeChallenge(verifier)
      expect(challenge).toMatch(/^[A-Za-z0-9_-]+$/)
      expect(challenge).not.toContain('+')
      expect(challenge).not.toContain('/')
      expect(challenge).not.toContain('=')
    })
  })

  describe('savePkceForCallback / getPkceFromStorage', () => {
    beforeEach(() => {
      sessionStorage.clear()
      vi.useRealTimers()
    })

    afterEach(() => {
      sessionStorage.clear()
      vi.useRealTimers()
    })

    it('round-trips PKCE with storedAtMs and returns it while fresh', () => {
      const before = Date.now()
      savePkceForCallback('st', 'verifier', {
        redirectUri: 'https://app/cb',
        tokenEndpoint: 'https://auth/token',
        clientId: 'cid'
      })
      const got = getPkceFromStorage()
      expect(got).not.toBeNull()
      expect(got!.state).toBe('st')
      expect(got!.code_verifier).toBe('verifier')
      expect(got!.storedAtMs).toBeGreaterThanOrEqual(before)
      expect(got!.storedAtMs).toBeLessThanOrEqual(Date.now())
    })

    it('returns null and clears storage when older than max age', () => {
      vi.useFakeTimers()
      const t0 = Date.now()
      vi.setSystemTime(t0)
      savePkceForCallback('st', 'verifier', {
        redirectUri: 'https://app/cb',
        tokenEndpoint: 'https://auth/token',
        clientId: 'cid'
      })
      vi.setSystemTime(t0 + PKCE_STORAGE_MAX_AGE_MS + 1)
      expect(getPkceFromStorage()).toBeNull()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })

    it('returns null and clears storage when storedAtMs is missing (legacy blob)', () => {
      sessionStorage.setItem(
        'oidc_co_pkce',
        JSON.stringify({
          state: 's',
          code_verifier: 'v',
          redirect_uri: 'https://r',
          token_endpoint: 'https://t',
          client_id: 'c'
        })
      )
      expect(getPkceFromStorage()).toBeNull()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })

    it('clearPkceStorage removes the key', () => {
      savePkceForCallback('st', 'verifier', {
        redirectUri: 'https://app/cb',
        tokenEndpoint: 'https://auth/token',
        clientId: 'cid'
      })
      clearPkceStorage()
      expect(sessionStorage.getItem('oidc_co_pkce')).toBeNull()
    })
  })
})
