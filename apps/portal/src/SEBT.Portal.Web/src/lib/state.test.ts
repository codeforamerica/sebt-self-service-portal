import { afterEach, describe, expect, it } from 'vitest'

import { getState, getStateAssetPath, getStateConfig, getStateName } from '@sebt/design-system'

describe('state', () => {
  const originalEnv = process.env.NEXT_PUBLIC_STATE

  afterEach(() => {
    process.env.NEXT_PUBLIC_STATE = originalEnv
  })

  describe('getState', () => {
    it('returns the env value as lowercase StateCode', () => {
      process.env.NEXT_PUBLIC_STATE = 'co'
      expect(getState()).toBe('co')
    })

    it('normalizes uppercase env values to lowercase', () => {
      process.env.NEXT_PUBLIC_STATE = 'CO'
      expect(getState()).toBe('co')
    })

    it('defaults to dc when env is undefined', () => {
      delete process.env.NEXT_PUBLIC_STATE
      expect(getState()).toBe('dc')
    })

    it('defaults to dc when env is empty string', () => {
      process.env.NEXT_PUBLIC_STATE = ''
      expect(getState()).toBe('dc')
    })
  })

  describe('getStateConfig', () => {
    it('returns DC config', () => {
      const config = getStateConfig('dc')
      expect(config.name).toBe('District of Columbia')
      expect(config.sealAlt).toBe('Government of the District of Columbia - Muriel Bowser, Mayor')
    })

    it('returns CO config', () => {
      const config = getStateConfig('co')
      expect(config.name).toBe('Colorado')
      expect(config.sealAlt).toBe('Colorado Official State Web Portal')
      expect(config.languageSelectorClass).toBe('border-primary radius-md text-primary')
      expect(config.languageSubmenuClass).toBe('bg-primary-dark')
    })

    it('returns undefined for optional CSS classes on DC', () => {
      const config = getStateConfig('dc')
      expect(config.languageSelectorClass).toBeUndefined()
      expect(config.languageSubmenuClass).toBeUndefined()
    })
  })

  describe('getStateName', () => {
    it('returns full name for dc', () => {
      expect(getStateName('dc')).toBe('District of Columbia')
    })

    it('returns full name for co', () => {
      expect(getStateName('co')).toBe('Colorado')
    })
  })

  describe('getStateAssetPath', () => {
    it('builds correct asset path for dc', () => {
      expect(getStateAssetPath('dc', 'seal.svg')).toBe('/images/states/dc/seal.svg')
    })

    it('builds correct asset path for co', () => {
      expect(getStateAssetPath('co', 'icons/translate_Rounded.svg')).toBe(
        '/images/states/co/icons/translate_Rounded.svg'
      )
    })
  })
})
