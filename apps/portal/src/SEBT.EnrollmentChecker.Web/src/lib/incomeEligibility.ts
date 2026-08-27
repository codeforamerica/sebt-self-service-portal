// Income screening maths. The figures themselves are runtime configuration served
// by the features endpoint, so nothing here hardcodes a threshold.

export interface IncomeEligibility {
  /** Annual gross income threshold for a household of one. */
  baseThreshold: number
  /** Added to the threshold for each member beyond the first. */
  perMemberIncrement: number
  /** Largest household size the selector offers. */
  maxHouseholdSize: number
}

/** Annual gross income below which a household of `size` is likely eligible. */
export function incomeThresholdFor(config: IncomeEligibility, size: number): number {
  return config.baseThreshold + (size - 1) * config.perMemberIncrement
}

/** Whole-dollar currency, matching how the threshold reads in the content. */
export function formatThreshold(amount: number, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0
  }).format(amount)
}
