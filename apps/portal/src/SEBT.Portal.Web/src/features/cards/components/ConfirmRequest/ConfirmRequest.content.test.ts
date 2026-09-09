import { describe, expect, it } from 'vitest'

import amDcOptionalId from '@/content/locales/am/dc/optionalId.json'
import amDcResult from '@/content/locales/am/dc/result.json'
import enCoOptionalId from '@/content/locales/en/co/optionalId.json'
import enCoResult from '@/content/locales/en/co/result.json'
import enDcOptionalId from '@/content/locales/en/dc/optionalId.json'
import enDcResult from '@/content/locales/en/dc/result.json'
import esCoOptionalId from '@/content/locales/es/co/optionalId.json'
import esCoResult from '@/content/locales/es/co/result.json'
import esDcOptionalId from '@/content/locales/es/dc/optionalId.json'
import esDcResult from '@/content/locales/es/dc/result.json'

// Content guard for the card-replacement copy that flows through
// fillCardPlaceholders() in ConfirmRequest. That helper only knows how to
// substitute [First name], [M.], [Last name], and [9999]; any line still
// holding a bracket after substitution is dropped rather than shown raw. So a
// translation whose [token] set differs from its English source either renders
// the wrong copy or silently disappears from the page (e.g. the DC Spanish
// pre-title shipping with the CO "[9999]" template instead of the name tokens,
// which makes the pre-title above the H1 vanish for DC).
//
// This asserts placeholder parity per state across languages. It is scoped to
// the three fill-driven keys on purpose — broad locale rows use richer bracket
// patterns (optional [s], nested name lists) that legitimately differ between
// languages and are never run through fillCardPlaceholders.
type ParityCase = {
  id: string
  english: string
  translations: { lang: string; value: string }[]
}

// DC optionalId.cardNumber is intentionally absent: it is empty for DC (only CO
// shows a card number), so there is no English baseline to anchor against.
const CASES: ParityCase[] = [
  {
    id: 'dc result.pre-title',
    english: enDcResult['pre-title'],
    translations: [
      { lang: 'es', value: esDcResult['pre-title'] },
      { lang: 'am', value: amDcResult['pre-title'] }
    ]
  },
  {
    id: "dc optionalId.who'sCard",
    english: enDcOptionalId["who'sCard"],
    translations: [
      { lang: 'es', value: esDcOptionalId["who'sCard"] },
      { lang: 'am', value: amDcOptionalId["who'sCard"] }
    ]
  },
  {
    id: 'co result.pre-title',
    english: enCoResult['pre-title'],
    translations: [{ lang: 'es', value: esCoResult['pre-title'] }]
  },
  {
    id: "co optionalId.who'sCard",
    english: enCoOptionalId["who'sCard"],
    translations: [{ lang: 'es', value: esCoOptionalId["who'sCard"] }]
  },
  {
    id: 'co optionalId.cardNumber',
    english: enCoOptionalId['cardNumber'],
    translations: [{ lang: 'es', value: esCoOptionalId['cardNumber'] }]
  }
]

function placeholderTokens(value: string): string[] {
  return [...new Set(value.match(/\[[^\]]+\]/g) ?? [])].sort()
}

describe('card-replacement copy placeholder parity (DC-461)', () => {
  for (const { id, english, translations } of CASES) {
    const englishTokens = placeholderTokens(english)
    for (const { lang, value } of translations) {
      it(`${id} carries the same placeholders as English (${lang})`, () => {
        expect(placeholderTokens(value)).toEqual(englishTokens)
      })
    }
  }
})
