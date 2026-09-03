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

// The figure to swap out of the authored income sentence. Some translations
// bracket it and some do not; the bracketed branch is first so it takes the
// brackets with it. Exported so the content test can assert every translation
// against the same pattern.
// TODO: Swap out with real i18next interpolation once GSheets source has been updated.
export const AUTHORED_FIGURE = /\[\s*\$[\d,]+\s*\]|\$[\d,]+/

/** The authored income sentence with its first figure replaced by `threshold`. */
export function withThreshold(sentence: string, threshold: string): string {
  // A replacer function, not a string: `$` runs carry meaning in a replacement.
  return sentence.replace(AUTHORED_FIGURE, () => threshold)
}
