import { describe, expect, it } from 'vitest'
import { getIncomeThreshold, HOUSEHOLD_SIZE_MAX } from './incomeThresholds'

describe('getIncomeThreshold', () => {
  describe('explicit table values (sizes 1-8)', () => {
    it('returns 28953 for size 1', () => {
      expect(getIncomeThreshold(1)).toBe(28953)
    })

    it('returns 39128 for size 2', () => {
      expect(getIncomeThreshold(2)).toBe(39128)
    })

    it('returns 49303 for size 3', () => {
      expect(getIncomeThreshold(3)).toBe(49303)
    })

    it('returns 59478 for size 4', () => {
      expect(getIncomeThreshold(4)).toBe(59478)
    })

    it('returns 69653 for size 5', () => {
      expect(getIncomeThreshold(5)).toBe(69653)
    })

    it('returns 79828 for size 6', () => {
      expect(getIncomeThreshold(6)).toBe(79828)
    })

    it('returns 90003 for size 7', () => {
      expect(getIncomeThreshold(7)).toBe(90003)
    })

    it('returns 100178 for size 8', () => {
      expect(getIncomeThreshold(8)).toBe(100178)
    })
  })

  describe('derived values (sizes 9-20)', () => {
    it('returns 110353 for size 9', () => {
      expect(getIncomeThreshold(9)).toBe(110353)
    })

    it('returns 120528 for size 10', () => {
      expect(getIncomeThreshold(10)).toBe(120528)
    })

    it('returns 130703 for size 11', () => {
      expect(getIncomeThreshold(11)).toBe(130703)
    })

    it('returns 140878 for size 12', () => {
      expect(getIncomeThreshold(12)).toBe(140878)
    })

    it('returns 151053 for size 13', () => {
      expect(getIncomeThreshold(13)).toBe(151053)
    })

    it('returns 161228 for size 14', () => {
      expect(getIncomeThreshold(14)).toBe(161228)
    })

    it('returns 171403 for size 15', () => {
      expect(getIncomeThreshold(15)).toBe(171403)
    })

    it('returns 181578 for size 16', () => {
      expect(getIncomeThreshold(16)).toBe(181578)
    })

    it('returns 191753 for size 17', () => {
      expect(getIncomeThreshold(17)).toBe(191753)
    })

    it('returns 201928 for size 18', () => {
      expect(getIncomeThreshold(18)).toBe(201928)
    })

    it('returns 212103 for size 19', () => {
      expect(getIncomeThreshold(19)).toBe(212103)
    })

    it('returns 222278 for size 20', () => {
      expect(getIncomeThreshold(20)).toBe(222278)
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
