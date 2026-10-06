import { describe, expect, it } from 'vitest'
import {
  bracketImbalance,
  hasBraceMismatch,
  hasOddBoldMarkers,
  unfillableTemplate,
  validateStateContent
} from './validate-content.js'

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

describe('unfillableTemplate', () => {
  it('accepts plural markers before and after the example list', () => {
    expect(unfillableTemplate('A replacement for the card[s] ending in [[9999], [9999],] card[s]')).toBeNull()
  })

  it('accepts bracketed names inside the example list', () => {
    expect(
      unfillableTemplate('A replacement for [[First name] [Last name], and [First name] [Last name]] card[s]')
    ).toBeNull()
  })

  it('reports a plural marker nested inside another bracket', () => {
    expect(
      unfillableTemplate('Reemplazo de la tarjeta[s] [terminada[s]] en [[9999], [9999],] será enviada')
    ).toMatch(/outside the example list/)
  })

  it('reports a bracket pair that straddles the example list', () => {
    expect(unfillableTemplate('tarjeta[s [[9999], [9999],] terminadas] en')).toMatch(
      /outside the example list/
    )
  })
})

describe('hasOddBoldMarkers', () => {
  it('accepts paired markers', () => {
    expect(hasOddBoldMarkers('sign in using **your account** or a **new one**')).toBe(false)
  })

  it('flags a value that lost one marker', () => {
    expect(hasOddBoldMarkers('si tu estudiante no cumple con los criterios anteriores y**')).toBe(true)
  })

  it('flags a pair split across a paragraph break, which never renders as bold', () => {
    expect(hasOddBoldMarkers('**Bold one\n\nand bold** two')).toBe(true)
  })

  it('accepts pairs that each close inside their own paragraph', () => {
    expect(hasOddBoldMarkers('**First** paragraph\n\n**Second** paragraph')).toBe(false)
  })
})

describe('hasBraceMismatch', () => {
  it('accepts interpolation placeholders', () => {
    expect(hasBraceMismatch('{{count}} more entries in {{year}}')).toBe(false)
  })

  it('accepts single-brace placeholders', () => {
    expect(hasBraceMismatch('Summer EBT in {state} for {year}')).toBe(false)
  })

  it('flags an extra closing brace', () => {
    expect(hasBraceMismatch('({count}} more entries')).toBe(true)
  })

  it('flags two broken placeholders whose brace counts cancel out', () => {
    expect(hasBraceMismatch('Hello {name}} and {{x}')).toBe(true)
  })
})

describe('validateStateContent', () => {
  it('returns nothing for clean content', () => {
    const data = stateData({
      en: { title: 'Card[s] ending in [[9999], [9999],] for {{count}}' },
      es: { title: 'Tarjeta[s] terminadas en [[9999], [9999],] para {{count}}' },
      am: { title: 'ካርድ[ዎች] [[9999], [9999],] {{count}}' }
    })

    expect(validateStateContent(data, 'dc')).toEqual({ errors: [] })
  })

  it('reports unbalanced brackets in a translation of bracketed English copy', () => {
    const data = stateData({
      en: { alertAddressTitle: 'Cards ending in [[9999], [9999],] will be sent' },
      es: { alertAddressTitle: 'Las tarjeta terminadas en [[9999], [9999], van a ser' }
    })

    const { errors } = validateStateContent(data, 'co')

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

    const { errors } = validateStateContent(data, 'co')

    expect(errors).toEqual([])
  })

  it('reports a translation that drops the example list the English copy carries', () => {
    const data = stateData({
      en: { alertAddressTitle: 'A replacement for [[First name] [Last name],] card[s]' },
      es: { alertAddressTitle: 'Una tarjeta de reemplazo para [First name] [Last name]' }
    })

    const { errors } = validateStateContent(data, 'dc')

    expect(rules(errors)).toEqual(['missing-example-list'])
    expect(errors[0]).toMatchObject({ locale: 'es', key: 'dashboard.alertAddressTitle' })
  })

  it('reports odd bold markers and mismatched braces as errors', () => {
    const data = stateData({
      en: { body: 'do **not meet the criteria**', more: '({count}} more entries' },
      es: { body: 'no cumple con los criterios y**', more: '{{count}} más' }
    })

    const { errors } = validateStateContent(data, 'dc')

    expect(errors.map((e) => `${e.locale} ${e.key} ${e.rule}`).sort()).toEqual([
      'en dashboard.more mismatched-braces',
      'es dashboard.body odd-bold-markers'
    ])
  })

  it('reports a translation whose brackets balance but cannot be filled', () => {
    const data = stateData({
      en: { alertAddressTitle: 'A replacement for the card[s] ending in [[9999], [9999],] will be sent' },
      es: {
        alertAddressTitle: 'Reemplazo de la tarjeta[s] [terminada[s]] en [[9999], [9999],] será enviada'
      }
    })

    const { errors } = validateStateContent(data, 'co')

    expect(errors).toEqual([
      expect.objectContaining({
        rule: 'unfillable-template',
        locale: 'es',
        key: 'dashboard.alertAddressTitle'
      })
    ])
  })

  it('skips empty values', () => {
    const data = stateData({ en: { title: 'Title' }, es: { title: '' } })

    expect(validateStateContent(data, 'co')).toEqual({ errors: [] })
  })
})

describe('validateStateContent for a key no code renders', () => {
  it('reports the defect as an error, because it is still a defect in the sheet', () => {
    // The defect that reached production: one opening brace lost from "{{name}}".
    const data = stateData({
      en: { unused: 'Hello {name}}' },
      es: { unused: 'Hola {{name}}' }
    })

    const result = validateStateContent(data, 'dc')

    expect(result).toEqual({
      errors: [expect.objectContaining({ rule: 'mismatched-braces', locale: 'en', key: 'dashboard.unused' })]
    })
  })
})
