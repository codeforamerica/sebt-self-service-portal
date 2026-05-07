export const INCOME_THRESHOLDS = [28953, 39128, 49303, 59478, 69653, 79828, 90003, 100178] as const

export const HOUSEHOLD_SIZE_MAX = 20

const PER_ADDITIONAL_MEMBER_INCREMENT = 10175

export function getIncomeThreshold(size: number): number {
  if (!Number.isInteger(size) || size < 1 || size > HOUSEHOLD_SIZE_MAX) {
    throw new RangeError(`household size ${size} is out of range (1-${HOUSEHOLD_SIZE_MAX})`)
  }
  if (size <= 8) return INCOME_THRESHOLDS[size - 1]
  const baseThreshold = INCOME_THRESHOLDS[INCOME_THRESHOLDS.length - 1]
  return baseThreshold + PER_ADDITIONAL_MEMBER_INCREMENT * (size - 8)
}
