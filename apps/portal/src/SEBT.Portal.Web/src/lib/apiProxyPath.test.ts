import { describe, expect, it } from 'vitest'

import { resolveApiProxyUrl } from './apiProxyPath'

const BACKEND = 'http://localhost:5280'

describe('resolveApiProxyUrl', () => {
  describe('legitimate paths', () => {
    it('builds the backend URL for normal proxied routes', () => {
      expect(resolveApiProxyUrl(['features'], BACKEND, '')?.pathname).toBe('/api/features')
      expect(resolveApiProxyUrl(['auth', 'status'], BACKEND, '')?.pathname).toBe('/api/auth/status')
      expect(resolveApiProxyUrl(['enrollment', 'check'], BACKEND, '')?.pathname).toBe(
        '/api/enrollment/check'
      )
    })

    it('maps an empty catch-all to /api', () => {
      expect(resolveApiProxyUrl(undefined, BACKEND, '')?.pathname).toBe('/api')
      expect(resolveApiProxyUrl([], BACKEND, '')?.pathname).toBe('/api')
    })

    it('preserves the query string', () => {
      const url = resolveApiProxyUrl(['features'], BACKEND, '?state=dc&year=2026')
      expect(url?.search).toBe('?state=dc&year=2026')
      expect(url?.toString()).toBe('http://localhost:5280/api/features?state=dc&year=2026')
    })

    it('does not over-block segments that merely contain a dot', () => {
      expect(resolveApiProxyUrl(['households', 'v1.2'], BACKEND, '')?.pathname).toBe(
        '/api/households/v1.2'
      )
      expect(
        resolveApiProxyUrl(['.well-known', 'openid-configuration'], BACKEND, '')?.pathname
      ).toBe('/api/.well-known/openid-configuration')
    })
  })

  describe('literal dot-segments', () => {
    it('rejects "." and ".." whole segments', () => {
      expect(resolveApiProxyUrl(['.'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['..'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['auth', '..', '..', 'health'], BACKEND, '')).toBeNull()
    })
  })

  describe('decoded traversal smuggled inside a single segment', () => {
    it('rejects slash-and-dot traversal that Next.js already decoded', () => {
      expect(resolveApiProxyUrl(['auth/../../health'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['x/../../health'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['features/../../health'], BACKEND, '')).toBeNull()
    })
  })

  describe('URL-encoded traversal', () => {
    it('rejects encoded slashes wrapping ".." (the reported exploit)', () => {
      expect(resolveApiProxyUrl(['auth%2F..%2F..%2Fhealth'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['x%2F..%2F..%2Fhealth'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['auth%2F..%2F..%2Fswagger%2Findex.html'], BACKEND, '')).toBeNull()
      expect(
        resolveApiProxyUrl(['auth%2F..%2F..%2Fswagger%2Fv1%2Fswagger.json'], BACKEND, '')
      ).toBeNull()
    })

    it('rejects encoded dots (%2e), mixed forms, and double-encoding', () => {
      expect(resolveApiProxyUrl(['%2e%2e'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['%2e.'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['.%2e'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['x%2e%2e%2fhealth'], BACKEND, '')).toBeNull()
      // Double-encoded slash still leaves a literal ".." after one decode.
      expect(resolveApiProxyUrl(['x%252F..%252F..%252Fhealth'], BACKEND, '')).toBeNull()
    })
  })

  describe('embedded separators', () => {
    it('rejects raw and encoded backslashes', () => {
      expect(resolveApiProxyUrl(['x\\..\\health'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['x%5c..%5chealth'], BACKEND, '')).toBeNull()
    })

    it('rejects a lone embedded separator even without dots', () => {
      expect(resolveApiProxyUrl(['x%2Fhealth'], BACKEND, '')).toBeNull()
    })
  })

  describe('malformed encoding', () => {
    it('rejects segments that cannot be decoded', () => {
      expect(resolveApiProxyUrl(['%'], BACKEND, '')).toBeNull()
      expect(resolveApiProxyUrl(['%zz'], BACKEND, '')).toBeNull()
    })
  })
})
