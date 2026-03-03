import { describe, expect, it } from 'vitest'

import {
  buildAuthorizationUrl,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState
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
})
