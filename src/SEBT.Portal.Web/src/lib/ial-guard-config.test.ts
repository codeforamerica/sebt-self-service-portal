import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getCoIdProofingMaxAgeYearsRaw, isDebugRepeatOidcStepUp } from './ial-guard-config'

describe('ial-guard-config', () => {
  describe('isDebugRepeatOidcStepUp', () => {
    beforeEach(() => {
      vi.unstubAllEnvs()
    })

    afterEach(() => {
      vi.unstubAllEnvs()
    })

    it('is false when NODE_ENV is not development', () => {
      vi.stubEnv('NODE_ENV', 'test')
      vi.stubEnv('NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP', 'true')
      expect(isDebugRepeatOidcStepUp()).toBe(false)
    })

    it('is false when flag is not true', () => {
      vi.stubEnv('NODE_ENV', 'development')
      vi.stubEnv('NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP', 'false')
      expect(isDebugRepeatOidcStepUp()).toBe(false)
    })

    it('is true only in development with flag true', () => {
      vi.stubEnv('NODE_ENV', 'development')
      vi.stubEnv('NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP', 'true')
      expect(isDebugRepeatOidcStepUp()).toBe(true)
    })
  })

  describe('getCoIdProofingMaxAgeYearsRaw', () => {
    beforeEach(() => {
      vi.unstubAllEnvs()
    })

    afterEach(() => {
      vi.unstubAllEnvs()
    })

    it('returns undefined when unset', () => {
      vi.stubEnv('NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS', undefined)
      expect(getCoIdProofingMaxAgeYearsRaw()).toBeUndefined()
    })

    it('returns the env string when set', () => {
      vi.stubEnv('NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS', '7')
      expect(getCoIdProofingMaxAgeYearsRaw()).toBe('7')
    })
  })
})
