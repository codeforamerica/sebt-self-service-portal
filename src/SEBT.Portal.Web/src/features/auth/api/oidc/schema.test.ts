import { describe, expect, it } from 'vitest'

import {
  isSafeOidcStepUpReturnPath,
  OIDC_RETURN_PATH_MAX_LENGTH,
  OidcCompleteLoginResponseSchema
} from './schema'

describe('isSafeOidcStepUpReturnPath', () => {
  it('accepts relative path with query', () => {
    expect(isSafeOidcStepUpReturnPath('/profile/address')).toBe(true)
    expect(isSafeOidcStepUpReturnPath('/dash?q=1')).toBe(true)
  })

  it('rejects absolute and scheme-relative URLs', () => {
    expect(isSafeOidcStepUpReturnPath('https://evil.example/')).toBe(false)
    expect(isSafeOidcStepUpReturnPath('//evil.example/path')).toBe(false)
  })

  it('allows http in query but not in path segment', () => {
    expect(isSafeOidcStepUpReturnPath('/redirect?u=http://evil')).toBe(true)
    expect(isSafeOidcStepUpReturnPath('/http://evil')).toBe(false)
  })

  it('rejects backslash and newlines in path', () => {
    expect(isSafeOidcStepUpReturnPath('/path\\evil')).toBe(false)
    expect(isSafeOidcStepUpReturnPath('/pa\nth')).toBe(false)
  })

  it('rejects overlong strings', () => {
    expect(isSafeOidcStepUpReturnPath(`/${'a'.repeat(OIDC_RETURN_PATH_MAX_LENGTH)}`)).toBe(false)
  })
})

describe('OidcCompleteLoginResponseSchema', () => {
  it('parses token with safe returnUrl', () => {
    const parsed = OidcCompleteLoginResponseSchema.safeParse({
      token: 'jwt',
      returnUrl: '/dashboard'
    })
    expect(parsed.success).toBe(true)
    expect(parsed.data?.returnUrl).toBe('/dashboard')
  })

  it('rejects unsafe returnUrl', () => {
    const parsed = OidcCompleteLoginResponseSchema.safeParse({
      token: 'jwt',
      returnUrl: 'https://evil.example/'
    })
    expect(parsed.success).toBe(false)
  })

  it('allows missing returnUrl', () => {
    const parsed = OidcCompleteLoginResponseSchema.safeParse({ token: 'jwt' })
    expect(parsed.success).toBe(true)
  })
})
