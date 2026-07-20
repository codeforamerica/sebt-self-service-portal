'use client'

import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getIncomeThreshold, HOUSEHOLD_SIZE_MAX } from '../lib/incomeThresholds'

const HOUSEHOLD_SIZE_OPTIONS = Array.from(
  { length: HOUSEHOLD_SIZE_MAX },
  (_, i) => i + 1,
)

// en-US currency formatting is intentional for both locales: the existing
// Spanish CSV copy already uses "$59,478"-style values inline, so locale-aware
// formatting would produce a mismatch with the surrounding translated string.
const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

export function IncomeCalculator() {
  const { t } = useTranslation('result')
  const [householdSize, setHouseholdSize] = useState<string>('1')
  const formattedThreshold = currencyFormatter.format(getIncomeThreshold(Number(householdSize)))
  const rawAlert = t('applyForSebtAccordionBodyAlertIncome')

  // TODO: Use t('applyForSebtAccordionBodyAlertIncome', { threshold }) once the
  // Google Sheet for co.csv is updated to interpolate {{threshold}}. The CSV is
  // owned by content team and exported from Google Sheets; we cannot edit it
  // directly. See .claude/rules/localization.md.
  const alertText = rawAlert.replace(/\$[\d,]+/, formattedThreshold)

  return (
    <div data-testid="income-calculator">
      <p>{t('applyForSebtAccordionBody3')}</p>
      <div className="usa-form-group">
        <label
          className="usa-label"
          htmlFor="income-calculator-size"
        >
          {t('applyForSebtAccordionLabelSelectNumberPeople')}
        </label>
        <select
          id="income-calculator-size"
          className="usa-select"
          value={householdSize}
          onChange={(e) => setHouseholdSize(e.target.value)}
        >
          {HOUSEHOLD_SIZE_OPTIONS.map((size) => (
            <option
              key={size}
              value={String(size)}
            >
              {size}
            </option>
          ))}
        </select>
      </div>
      <div
        role="status"
        className="usa-alert usa-alert--info usa-alert--slim"
      >
        <div className="usa-alert__body">
          <p className="usa-alert__text">{alertText}</p>
        </div>
      </div>
    </div>
  )
}
