import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'
import { CHECKER_STATES } from './checkerAssetPath'
import type { IncomeEligibility } from './incomeEligibility'
import {
  AUTHORED_FIGURE,
  formatThreshold,
  incomeThresholdFor,
  withThreshold
} from './incomeEligibility'
import { getStateConfig } from '@sebt/design-system/src/lib/state'

// Stands in for whatever the features endpoint serves; the maths must not care.
const config: IncomeEligibility = {
  baseThreshold: 28953,
  perMemberIncrement: 10175,
  maxHouseholdSize: 8
}

describe('incomeThresholdFor', () => {
  it('uses the base threshold for a household of one', () => {
    expect(incomeThresholdFor(config, 1)).toBe(28953)
  })

  it('adds one increment per additional member', () => {
    expect(incomeThresholdFor(config, 4)).toBe(59478)
    expect(incomeThresholdFor(config, 8)).toBe(100178)
  })

  it('rises by exactly the increment between consecutive sizes', () => {
    for (let size = 2; size <= config.maxHouseholdSize; size++) {
      expect(incomeThresholdFor(config, size) - incomeThresholdFor(config, size - 1)).toBe(
        config.perMemberIncrement
      )
    }
  })

  it('tracks whatever figures it is handed', () => {
    const nextYear: IncomeEligibility = {
      baseThreshold: 30000,
      perMemberIncrement: 11000,
      maxHouseholdSize: 10
    }
    expect(incomeThresholdFor(nextYear, 3)).toBe(52000)
  })
})

describe('formatThreshold', () => {
  it('renders whole dollars with a thousands separator', () => {
    expect(formatThreshold(59478, 'en')).toBe('$59,478')
  })

  it('rounds away cents rather than showing them', () => {
    expect(formatThreshold(28953.4, 'en')).toBe('$28,953')
  })

  // Plain 'es' renders 69653 as "69.653 US$", which reads as sixty-nine dollars
  // to a US audience.
  it('renders Spanish in US conventions', () => {
    expect(formatThreshold(69653, 'es')).toBe('$69,653')
  })

  it('renders Amharic with an unambiguous currency prefix', () => {
    expect(formatThreshold(69653, 'am')).toBe('US$69,653')
  })

  it('reads the language from a regional locale', () => {
    expect(formatThreshold(69653, 'es-419')).toBe(formatThreshold(69653, 'es'))
  })

  it('falls back to US formatting for an unmapped language', () => {
    expect(formatThreshold(69653, 'fr')).toBe('$69,653')
  })

  // The hazard is a dot as the thousands separator: it turns a five-figure
  // threshold into a two-figure one.
  it.each(['en', 'es', 'am'])('groups thousands with a comma in %s', (lang) => {
    expect(formatThreshold(69653, lang)).toContain('69,653')
  })
})

describe('withThreshold', () => {
  it('replaces a bracketed figure, brackets and all', () => {
    expect(withThreshold('less than [$28,953] every year', '$59,478')).toBe(
      'less than $59,478 every year'
    )
  })

  // Spanish authors the figure without brackets.
  it('replaces a bare figure', () => {
    expect(withThreshold('menos de $28,953 cada año', '$59,478')).toBe(
      'menos de $59,478 cada año'
    )
  })

  it('keeps the threshold verbatim when it contains $ run characters', () => {
    expect(withThreshold('less than [$1] every year', '$59,478')).toBe(
      'less than $59,478 every year'
    )
  })

  it('replaces only the first figure', () => {
    expect(withThreshold('[$1] and $2', '$9')).toBe('$9 and $2')
  })

  it('leaves a sentence with no figure untouched', () => {
    expect(withThreshold('no figure here', '$59,478')).toBe('no figure here')
  })
})

// The substitution is silent: a translation this pattern misses keeps the figure
// its author typed, ignoring both the household size and the configured
// thresholds. Nothing at runtime reports that, so it has to fail here.
describe('authored income sentence', () => {
  const KEY = 'applyForSebtAccordionBodyAlertIncome'

  for (const state of CHECKER_STATES) {
    for (const lang of getStateConfig(state).supportedLanguages) {
      it(`carries a substitutable figure in ${lang}/${state}`, () => {
        const file = path.join(__dirname, '../../content/locales', lang, state, 'result.json')
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        if (!existsSync(file)) {
          return
        }
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        const copy = JSON.parse(readFileSync(file, 'utf8')) as Record<string, string>
        // eslint-disable-next-line security/detect-object-injection -- KEY is a literal
        const sentence = copy[KEY]
        if (!sentence) {
          return
        }

        expect(
          AUTHORED_FIGURE.test(sentence),
          `${lang}/${state} ${KEY} has no figure to substitute: ${sentence}`
        ).toBe(true)
      })
    }
  }
})
