import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { isDebugRepeatOidcStepUp } from './ial-guard-config'

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
      expect(isDebugRepeatOidcStepUp(true)).toBe(false)
    })

    it('is false when the flag is off', () => {
      vi.stubEnv('NODE_ENV', 'development')
      expect(isDebugRepeatOidcStepUp(false)).toBe(false)
    })

    it('is true only in development with the flag on', () => {
      vi.stubEnv('NODE_ENV', 'development')
      expect(isDebugRepeatOidcStepUp(true)).toBe(true)
    })
  })
})
