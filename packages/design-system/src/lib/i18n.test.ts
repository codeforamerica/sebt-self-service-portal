import { describe, expect, it } from 'vitest'

import { supportedLanguages } from './i18n'
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
