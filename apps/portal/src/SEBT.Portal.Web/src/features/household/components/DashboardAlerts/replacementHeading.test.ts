import { describe, expect, it } from 'vitest'

import amDcDashboard from '@/content/locales/am/dc/dashboard.json'
import enCoDashboard from '@/content/locales/en/co/dashboard.json'
import enDcDashboard from '@/content/locales/en/dc/dashboard.json'
import esCoDashboard from '@/content/locales/es/co/dashboard.json'
import esDcDashboard from '@/content/locales/es/dc/dashboard.json'

import { fillReplacementHeading } from './replacementHeading'

describe('fillReplacementHeading', () => {
  it('fills the DC name-list template for multiple cards', () => {
    expect(
      fillReplacementHeading(
        enDcDashboard.alertAddressTitle,
        ['Jane Doe', 'John Doe', 'Ana Roe'],
        'en'
      )
    ).toBe(
      'A replacement for Jane Doe, John Doe, and Ana Roe cards will be sent to the following address'
    )
  })

  it('drops the plural marker for a single card', () => {
    expect(fillReplacementHeading(enDcDashboard.alertAddressTitle, ['Jane Doe'], 'en')).toBe(
      'A replacement for Jane Doe card will be sent to the following address'
    )
  })

  it('fills the CO digit-list template for multiple cards', () => {
    expect(fillReplacementHeading(enCoDashboard.alertAddressTitle, ['1234', '5678'], 'en')).toBe(
      'A replacement for the cards ending in 1234 and 5678 will be sent to the following address'
    )
  })

  it('fills the CO digit-list template for a single card', () => {
    expect(fillReplacementHeading(enCoDashboard.alertAddressTitle, ['1234'], 'en')).toBe(
      'A replacement for the card ending in 1234 will be sent to the following address'
    )
  })

  it('returns null when there are no items to list', () => {
    expect(fillReplacementHeading(enDcDashboard.alertAddressTitle, [], 'en')).toBeNull()
  })

  it('returns null when the template has no example-list block', () => {
    expect(
      fillReplacementHeading('A replacement for [First name] cards will be sent', ['Jane'], 'en')
    ).toBeNull()
  })

  it('returns null when the example-list block never closes', () => {
    expect(
      fillReplacementHeading('Cards ending in [[9999], [9999] will be sent', ['1234'], 'en')
    ).toBeNull()
  })

  it('never leaks raw brackets for any locale template', () => {
    const templates = [
      ['en', enDcDashboard.alertAddressTitle],
      ['en', enCoDashboard.alertAddressTitle],
      ['es', esDcDashboard.alertAddressTitle],
      ['es', esCoDashboard.alertAddressTitle],
      ['am', amDcDashboard.alertAddressTitle]
    ] as const

    for (const [language, template] of templates) {
      const result = fillReplacementHeading(template, ['1234', '5678'], language)
      if (result !== null) {
        expect(result).not.toMatch(/[[\]]/)
      }
    }
  })
})
