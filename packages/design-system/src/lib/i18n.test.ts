import { describe, expect, it } from 'vitest'

import i18n, { initI18n, supportedLanguages, type StateResources } from './i18n'
import { getState, getStateConfig } from './state'

describe('i18n — supportedLanguages export', () => {
  it('matches the current state config', () => {
    expect(supportedLanguages).toEqual(getStateConfig(getState()).supportedLanguages)
  })

  it('is the readonly list of language codes, not the whole config object', () => {
    expect(Array.isArray(supportedLanguages)).toBe(true)
    expect(supportedLanguages.every((code) => typeof code === 'string')).toBe(true)
  })
})

describe('i18n — initI18n', () => {
  const resources: StateResources = {
    co: { en: { common: { greeting: 'Hello {{stateName}} ({{state}})' } } }
  }

  it('throws when the state has no generated bundle instead of falling back to another state', () => {
    expect(() => initI18n(resources, ['common'], 'dc')).toThrow(/dc/)
  })

  it("interpolates the state's display name from the state config", async () => {
    initI18n(resources, ['common'], 'co')
    await i18n.changeLanguage('en')

    expect(i18n.t('greeting')).toBe(`Hello ${getStateConfig('co').name} (CO)`)
  })
})
