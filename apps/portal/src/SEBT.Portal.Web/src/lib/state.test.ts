import { afterEach, describe, expect, it } from 'vitest'

import { getState, getStateAssetPath, getStateConfig, getStateName } from '@sebt/design-system'

describe('state', () => {
  const originalState = process.env.STATE
  const originalPublicState = process.env.NEXT_PUBLIC_STATE

  afterEach(() => {
    process.env.STATE = originalState
    process.env.NEXT_PUBLIC_STATE = originalPublicState
    delete document.documentElement.dataset.state
  })

  describe('getState', () => {
    // The portal ships one artifact for every state, so the browser's only source
    // is the attribute the server stamped on <html>. It has to outrank the build
    // env or client components would render the build's state, not the deployment's.
    it('prefers the data-state attribute the server stamped on <html>', () => {
      process.env.STATE = 'dc'
      document.documentElement.dataset.state = 'co'
      expect(getState()).toBe('co')
    })

    it('normalizes an uppercase data-state attribute', () => {
      document.documentElement.dataset.state = 'CO'
      expect(getState()).toBe('co')
    })

    it('falls back to STATE when no attribute is present', () => {
      delete document.documentElement.dataset.state
      process.env.STATE = 'co'
      expect(getState()).toBe('co')
    })

    it('normalizes uppercase env values to lowercase', () => {
      delete document.documentElement.dataset.state
      process.env.STATE = 'CO'
      expect(getState()).toBe('co')
    })

    // The enrollment checker still deploys one static export per state.
    it('falls back to NEXT_PUBLIC_STATE for per-state builds', () => {
      delete document.documentElement.dataset.state
      delete process.env.STATE
      process.env.NEXT_PUBLIC_STATE = 'co'
      expect(getState()).toBe('co')
    })

    it('defaults to dc when nothing is set', () => {
      delete document.documentElement.dataset.state
      delete process.env.STATE
      delete process.env.NEXT_PUBLIC_STATE
      expect(getState()).toBe('dc')
    })

    it('defaults to dc when env values are empty strings', () => {
      delete document.documentElement.dataset.state
      process.env.STATE = ''
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
