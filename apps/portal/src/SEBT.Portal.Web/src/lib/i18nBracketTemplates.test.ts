/**
 * Guard: bracket templates in copy fill cleanly in every language.
 *
 * Some locale values are templates the code fills at runtime. The
 * replacement-address heading (`dashboard.alertAddressTitle`) carries a
 * double-bracketed example list, `[[9999], [9999], and [9999],]`, that
 * `fillReplacementHeading` swaps for the real card numbers or names, plus
 * single-bracket plural markers such as `card[s]`.
 *
 * The convention is load-bearing and invisible to a translator. A Spanish cell
 * that reads perfectly well but drops one `]` makes the parser return null, and
 * the caller then omits the heading rather than print raw tokens. Nothing looks
 * broken, so neither a runtime walk nor a key-coverage check can see it. This is
 * how the Spanish heading vanished on both Colorado and DC.
 *
 * Two checks, both from the content side:
 * - every value in every shipped bundle has balanced brackets;
 * - every key whose English value carries an example list fills cleanly in
 *   every language of that state, for one item and for several.
 */
import { describe, expect, it } from 'vitest'

import { fillReplacementHeading } from '@/features/household/components/DashboardAlerts/replacementHeading'

import { loadBundles, type AppName } from './i18nContentScan'

/** Mirrors i18nKeyCoverage.test.ts: the states each app actually ships. */
const APP_STATES: Record<AppName, string[]> = {
  portal: ['dc', 'co'],
  checker: ['co']
}

interface Exemption {
  /** `state/lang namespace:key`, e.g. `co/es dashboard:alertAddressTitle`. */
  id: string
  reason: string
}

/**
 * Values whose brackets are unbalanced on purpose. Each entry asserts that the
 * value is never run through a bracket-filling helper and that the bracket is
 * meant to reach the reader as a character. If you cannot say that, fix the
 * content instead.
 */
const EXEMPT: Exemption[] = []

interface Offender {
  id: string
  problem: string
  value: string
}

const EXAMPLE_LIST = '[['
const SAMPLE_ITEMS: string[][] = [['1234'], ['1234', '5678']]

/** Explains why a value's brackets do not balance, or null when they do. */
function bracketImbalance(value: string): string | null {
  let depth = 0
  for (const char of value) {
    if (char === '[') {
      depth++
    } else if (char === ']') {
      depth--
      if (depth < 0) return 'closes a bracket that was never opened'
    }
  }
  return depth > 0 ? `leaves ${depth} bracket(s) unclosed` : null
}

function isExempt(id: string): boolean {
  return EXEMPT.some((e) => e.id === id)
}

function unbalancedValues(app: AppName): Offender[] {
  const bundles = loadBundles(app)
  const offenders: Offender[] = []
  for (const state of APP_STATES[app]) {
    const byLang = bundles[state] ?? {}
    for (const lang of Object.keys(byLang)) {
      for (const [ns, entries] of Object.entries(byLang[lang] ?? {})) {
        for (const [key, value] of Object.entries(entries)) {
          if (typeof value !== 'string') continue
          const id = `${state}/${lang} ${ns}:${key}`
          if (isExempt(id)) continue
          const problem = bracketImbalance(value)
          if (problem) offenders.push({ id, problem, value })
        }
      }
    }
  }
  return offenders
}

/**
 * Keys whose English value carries an example list, checked in every language
 * that state ships. English is the anchor because the sheet's source column is
 * English and the code was written against it.
 */
function unfillableTemplates(app: AppName): Offender[] {
  const bundles = loadBundles(app)
  const offenders: Offender[] = []
  for (const state of APP_STATES[app]) {
    const byLang = bundles[state] ?? {}
    const english = byLang['en'] ?? {}
    for (const [ns, entries] of Object.entries(english)) {
      for (const [key, source] of Object.entries(entries)) {
        if (typeof source !== 'string' || !source.includes(EXAMPLE_LIST)) continue
        for (const lang of Object.keys(byLang)) {
          const value = byLang[lang]?.[ns]?.[key]
          // A missing translation is the key-coverage guard's finding, not this one's.
          if (typeof value !== 'string') continue
          const id = `${state}/${lang} ${ns}:${key}`
          if (isExempt(id)) continue
          for (const items of SAMPLE_ITEMS) {
            const filled = fillReplacementHeading(value, items, lang)
            if (filled === null) {
              offenders.push({ id, problem: `does not fill for ${items.length} item(s)`, value })
              break
            }
            if (/[[\]]/.test(filled)) {
              offenders.push({ id, problem: `leaves raw brackets: ${filled}`, value })
              break
            }
          }
        }
      }
    }
  }
  return offenders
}

function describeOffenders(offenders: Offender[]): string {
  return offenders.map((o) => `  ${o.id}\n      ${o.problem}\n      "${o.value}"`).join('\n')
}

describe.each(Object.keys(APP_STATES) as AppName[])(
  '%s: bracket templates fill in every language',
  (app) => {
    it('has no value with unbalanced brackets', () => {
      const offenders = unbalancedValues(app)
      expect(
        offenders,
        offenders.length
          ? `\n\nThese locale values have unbalanced brackets. Any code that fills the\n` +
              `template will drop the whole line rather than render it:\n\n${describeOffenders(offenders)}\n\n` +
              `Fix the cell in the content sheet so every [ has a matching ], or, if the\n` +
              `bracket is meant to reach the reader as a character, add an entry to EXEMPT\n` +
              `in this file explaining why.\n`
          : undefined
      ).toEqual([])
    })

    it('fills every example-list template for one item and for several', () => {
      const offenders = unfillableTemplates(app)
      expect(
        offenders,
        offenders.length
          ? `\n\nThese translations of an example-list template cannot be filled, so the\n` +
              `line is omitted from the page in that language:\n\n${describeOffenders(offenders)}\n\n` +
              `The list must open with [[ and close with ], with plural markers as\n` +
              `single [brackets] outside it. Compare the English value for the shape.\n`
          : undefined
      ).toEqual([])
    })

    it('has no stale exemption', () => {
      const bundles = loadBundles(app)
      const stale = EXEMPT.filter((e) => {
        const [where, path] = e.id.split(' ')
        const [state, lang] = (where ?? '').split('/')
        const [ns, key] = (path ?? '').split(':')
        const value = bundles[state ?? '']?.[lang ?? '']?.[ns ?? '']?.[key ?? '']
        return typeof value !== 'string' || bracketImbalance(value) === null
      })
      expect(stale.map((e) => e.id)).toEqual([])
    })
  }
)
