import { describe, expect, it } from 'vitest'
import type { IncomeEligibility } from './incomeEligibility'
import { formatThreshold, incomeThresholdFor } from './incomeEligibility'

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
})
