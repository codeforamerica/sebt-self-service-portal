import { describe, expect, it } from 'vitest'

import {
  classifyIdpOAuthRedirectError,
  sanitizeHumanOAuthErrorDetail,
  tryDecodeOAuthErrorDescription
} from './oidcCallbackErrors'

describe('oidcCallbackErrors', () => {
  describe('tryDecodeOAuthErrorDescription', () => {
    it('decodes URI-encoded descriptions', () => {
      expect(tryDecodeOAuthErrorDescription('User+cancelled')).toBe('User cancelled')
    })
  })

  describe('sanitizeHumanOAuthErrorDetail', () => {
    it('returns short human phrases', () => {
      expect(sanitizeHumanOAuthErrorDetail('User cancelled')).toBe('User cancelled')
    })

    it('rejects JSON-looking payloads', () => {
      expect(sanitizeHumanOAuthErrorDetail('{"error":"access_denied"}')).toBeUndefined()
    })
  })

  describe('classifyIdpOAuthRedirectError', () => {
    it('classifies Socure consent decline inside a Ping connector blob', () => {
      const blob = JSON.stringify({
        message: 'nested',
        errors: { x: { additionalProperties: { errorMsg: 'User opted out' } } }
      })
      const result = classifyIdpOAuthRedirectError('invalid_request', blob)
      expect(result).toEqual({ type: 'stepUpDeclined' })
    })

    it('classifies OAuth access_denied without reading description', () => {
      expect(classifyIdpOAuthRedirectError('access_denied', null)).toEqual({
        type: 'stepUpDeclined'
      })
    })

    it('returns idpRedirect without detail for structured payloads without consent signals', () => {
      const blob = JSON.stringify({
        interactionId: 'abc',
        message: 'Error creating delayed response'
      })
      expect(classifyIdpOAuthRedirectError('server_error', blob)).toEqual({
        type: 'idpRedirect'
      })
    })

    it('passes through short safe descriptions', () => {
      expect(classifyIdpOAuthRedirectError('server_error', 'User cancelled')).toEqual({
        type: 'idpRedirect',
        safeDetail: 'User cancelled'
      })
    })
  })
})
