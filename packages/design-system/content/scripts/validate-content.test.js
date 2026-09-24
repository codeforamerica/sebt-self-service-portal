import { describe, expect, it } from 'vitest'
import { bracketImbalance, hasBraceMismatch, hasOddBoldMarkers, validateStateContent } from './validate-content.js'

// Builds the generator's per-state shape: { locale: { namespace: { key: value } } }
function stateData({ en = {}, es = {}, am = {} }) {
  return { en: { dashboard: en }, es: { dashboard: es }, am: { dashboard: am } }
}

function rules(findings) {
  return findings.map((f) => f.rule)
}

describe('bracketImbalance', () => {
  it('accepts a nested example list with plural markers', () => {
    expect(
      bracketImbalance('A replacement for the card[s] ending in [[9999], [9999], and [9999],] will be sent')
    ).toBeNull()
  })

  it('reports an example list that never closes', () => {
    expect(bracketImbalance('Las tarjeta terminadas en [[9999], [9999], y [9999], van a ser')).toMatch(
      /unclosed/
    )
  })

  it('reports a bracket that closes before it opens', () => {
    expect(bracketImbalance('para [First name] [Last name]] a la siguiente')).toMatch(/never opened/)
  })
})

describe('hasOddBoldMarkers', () => {
  it('accepts paired markers', () => {
    expect(hasOddBoldMarkers('sign in using **your account** or a **new one**')).toBe(false)
  })

  it('flags a value that lost one marker', () => {
    expect(hasOddBoldMarkers('si tu estudiante no cumple con los criterios anteriores y**')).toBe(true)
  })
})

describe('hasBraceMismatch', () => {
  it('accepts interpolation placeholders', () => {
    expect(hasBraceMismatch('{{count}} more entries in {{year}}')).toBe(false)
  })

  it('flags an extra closing brace', () => {
    expect(hasBraceMismatch('({count}} more entries')).toBe(true)
  })
})

describe('validateStateContent', () => {
  it('returns nothing for clean content', () => {
    const data = stateData({
      en: { title: 'Card[s] ending in [[9999], [9999],] for {{count}}' },
      es: { title: 'Tarjeta[s] terminadas en [[9999], [9999],] para {{count}}' },
      am: { title: 'ካርድ[ዎች] [[9999], [9999],] {{count}}' }
    })

    expect(validateStateContent(data, 'dc', {})).toEqual({ errors: [], warnings: [] })
  })

  it('reports unbalanced brackets in a translation of bracketed English copy', () => {
    const data = stateData({
      en: { alertAddressTitle: 'Cards ending in [[9999], [9999],] will be sent' },
      es: { alertAddressTitle: 'Las tarjeta terminadas en [[9999], [9999], van a ser' }
    })

    const { errors } = validateStateContent(data, 'co', {})

    expect(errors).toEqual([
      expect.objectContaining({
        rule: 'unbalanced-brackets',
        state: 'co',
        locale: 'es',
        key: 'dashboard.alertAddressTitle'
      })
    ])
  })

  it('leaves a stray bracket alone when the English copy has no brackets', () => {
    const data = stateData({
      en: { note: 'Call us' },
      es: { note: 'Llámanos] hoy' }
    })

    const { errors } = validateStateContent(data, 'co', {})

    expect(errors).toEqual([])
  })

  it('reports a translation that drops the example list the English copy carries', () => {
    const data = stateData({
      en: { alertAddressTitle: 'A replacement for [[First name] [Last name],] card[s]' },
      es: { alertAddressTitle: 'Una tarjeta de reemplazo para [First name] [Last name]' }
    })

    const { errors } = validateStateContent(data, 'dc', {})

    expect(rules(errors)).toEqual(['missing-example-list'])
    expect(errors[0]).toMatchObject({ locale: 'es', key: 'dashboard.alertAddressTitle' })
  })

  it('reports odd bold markers and mismatched braces as errors', () => {
    const data = stateData({
      en: { body: 'do **not meet the criteria**', more: '({count}} more entries' },
      es: { body: 'no cumple con los criterios y**', more: '{{count}} más' }
    })

    const { errors, warnings } = validateStateContent(data, 'dc', {})

    expect(warnings).toEqual([])
    expect(errors.map((e) => `${e.locale} ${e.key} ${e.rule}`).sort()).toEqual([
      'en dashboard.more mismatched-braces',
      'es dashboard.body odd-bold-markers'
    ])
  })

  it('skips empty values', () => {
    const data = stateData({ en: { title: 'Title' }, es: { title: '' } })

    expect(validateStateContent(data, 'co', {})).toEqual({ errors: [], warnings: [] })
  })
})

describe('validateStateContent with isReferenced', () => {
  // The defect that reached production: one opening brace lost from "{{name}}".
  const data = stateData({
    en: { greeting: 'Hello {name}}', unused: 'Hello {name}}' },
    es: { greeting: 'Hola {{name}}', unused: 'Hola {{name}}' }
  })
  const isReferenced = (namespace, name) => namespace === 'dashboard' && name === 'greeting'

  it('fails on a "{text}}" placeholder in a key the app renders', () => {
    const { errors } = validateStateContent(data, 'dc', { isReferenced })

    expect(errors).toEqual([
      expect.objectContaining({ rule: 'mismatched-braces', locale: 'en', key: 'dashboard.greeting' })
    ])
  })

  it('only warns about the same defect in a key no code references', () => {
    const { warnings } = validateStateContent(data, 'dc', { isReferenced })

    expect(warnings).toEqual([
      expect.objectContaining({
        rule: 'mismatched-braces',
        locale: 'en',
        key: 'dashboard.unused',
        detail: expect.stringContaining('no app code references this key')
      })
    ])
  })

  it('keeps the defect an error for every key when isReferenced is not given', () => {
    const { errors } = validateStateContent(data, 'dc', {})

    expect(errors.map((e) => e.key).sort()).toEqual(['dashboard.greeting', 'dashboard.unused'])
  })
})
