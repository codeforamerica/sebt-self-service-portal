import { describe, expect, it } from 'vitest'
import { getIncomeThreshold, HOUSEHOLD_SIZE_MAX } from './incomeThresholds'

describe('getIncomeThreshold', () => {
  describe('explicit table values (sizes 1-8)', () => {
    it('returns 29526 for size 1', () => {
      expect(getIncomeThreshold(1)).toBe(29526)
    })

    it('returns 40034 for size 2', () => {
      expect(getIncomeThreshold(2)).toBe(40034)
    })

    it('returns 50542 for size 3', () => {
      expect(getIncomeThreshold(3)).toBe(50542)
    })

    it('returns 61050 for size 4', () => {
      expect(getIncomeThreshold(4)).toBe(61050)
    })

    it('returns 71558 for size 5', () => {
      expect(getIncomeThreshold(5)).toBe(71558)
    })

    it('returns 82066 for size 6', () => {
      expect(getIncomeThreshold(6)).toBe(82066)
    })

    it('returns 92574 for size 7', () => {
      expect(getIncomeThreshold(7)).toBe(92574)
    })

    it('returns 103082 for size 8', () => {
      expect(getIncomeThreshold(8)).toBe(103082)
    })
  })

  describe('derived values (sizes 9-20)', () => {
    it('returns 113590 for size 9', () => {
      expect(getIncomeThreshold(9)).toBe(113590)
    })

    it('returns 124098 for size 10', () => {
      expect(getIncomeThreshold(10)).toBe(124098)
    })

    it('returns 134606 for size 11', () => {
      expect(getIncomeThreshold(11)).toBe(134606)
    })

    it('returns 145114 for size 12', () => {
      expect(getIncomeThreshold(12)).toBe(145114)
    })

    it('returns 155622 for size 13', () => {
      expect(getIncomeThreshold(13)).toBe(155622)
    })

    it('returns 166130 for size 14', () => {
      expect(getIncomeThreshold(14)).toBe(166130)
    })

    it('returns 176638 for size 15', () => {
      expect(getIncomeThreshold(15)).toBe(176638)
    })

    it('returns 187146 for size 16', () => {
      expect(getIncomeThreshold(16)).toBe(187146)
    })

    it('returns 197654 for size 17', () => {
      expect(getIncomeThreshold(17)).toBe(197654)
    })

    it('returns 208162 for size 18', () => {
      expect(getIncomeThreshold(18)).toBe(208162)
    })

    it('returns 218670 for size 19', () => {
      expect(getIncomeThreshold(19)).toBe(218670)
    })

    it('returns 229178 for size 20', () => {
      expect(getIncomeThreshold(20)).toBe(229178)
    })
  })

  describe('invalid input throws RangeError', () => {
    it('throws for size 0', () => {
      expect(() => getIncomeThreshold(0)).toThrow(RangeError)
    })

    it('throws for negative size', () => {
      expect(() => getIncomeThreshold(-1)).toThrow(RangeError)
    })

    it('throws for non-integer size (1.5)', () => {
      expect(() => getIncomeThreshold(1.5)).toThrow(RangeError)
    })

    it('throws for NaN', () => {
      expect(() => getIncomeThreshold(NaN)).toThrow(RangeError)
    })

    it('throws for Infinity', () => {
      expect(() => getIncomeThreshold(Infinity)).toThrow(RangeError)
    })

    it('throws for size above cap (21)', () => {
      expect(() => getIncomeThreshold(21)).toThrow(RangeError)
    })
  })

  describe('HOUSEHOLD_SIZE_MAX', () => {
    it('is 20', () => {
      expect(HOUSEHOLD_SIZE_MAX).toBe(20)
    })
  })
})
