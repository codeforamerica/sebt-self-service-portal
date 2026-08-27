'use client'

import { RichText } from '@sebt/design-system'
import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { useCheckerFeatures } from '@/features/maintenance/hooks/useCheckerFeatures'
import { formatThreshold, incomeThresholdFor } from '@/lib/incomeEligibility'
import { getEnrollmentConfig } from '@/lib/stateConfig'

interface EligibilityAccordionProps {
  /** Application destination, or null when none is configured. */
  applyHref: string | null
}

// The income sentence is authored with the figure written in, bracketed to mark
// it as a value to substitute. Swapping on that bracketed run keeps the rest of
// the sentence — and its translations — under the content team's control.
const AUTHORED_FIGURE = /\[\s*\$[\d,]+\s*\]/

function withThreshold(sentence: string, threshold: string): string {
  return sentence.replace(AUTHORED_FIGURE, threshold)
}

/**
 * Explains who should apply, and screens income against household size.
 *
 * The threshold rises with each household member, so a single figure cannot
 * answer the question — the selector recomputes the sentence as the size
 * changes.
 */
export function EligibilityAccordion({ applyHref }: EligibilityAccordionProps) {
  const { t, i18n } = useTranslation('result')
  const [isExpanded, setIsExpanded] = useState(false)
  const [householdSize, setHouseholdSize] = useState(1)

  const contentId = useId()
  const selectId = useId()

  // Thresholds track federal poverty guidelines that change yearly, so they are
  // runtime configuration. Absent means the screening tool is withdrawn — the
  // explanatory copy still stands on its own.
  const { apiBaseUrl } = getEnrollmentConfig()
  const { data } = useCheckerFeatures(apiBaseUrl)
  const incomeEligibility = data?.incomeEligibility

  const sizes = incomeEligibility
    ? Array.from({ length: incomeEligibility.maxHouseholdSize }, (_, i) => i + 1)
    : []
  const threshold = incomeEligibility
    ? formatThreshold(incomeThresholdFor(incomeEligibility, householdSize), i18n.language)
    : null

  return (
    <div className="usa-accordion margin-top-3">
      <h2 className="usa-accordion__heading">
        <button
          type="button"
          className="usa-accordion__button"
          aria-expanded={isExpanded}
          aria-controls={contentId}
          onClick={() => setIsExpanded((prev) => !prev)}
        >
          {t('applyForSebtAccordionTitle')}
        </button>
      </h2>

      <div
        id={contentId}
        className="usa-accordion__content usa-prose"
        hidden={!isExpanded}
      >
        <RichText>{t('applyForSebtAccordionBody1')}</RichText>

        {applyHref && (
          <p>
            <a
              href={applyHref}
              data-analytics-cta="apply_cta"
              data-testid="accordion-apply-link"
            >
              {t('applyForSebtAccordionBody2')}
            </a>
          </p>
        )}

        {threshold && (
          <>
            <RichText>{t('applyForSebtAccordionBody3')}</RichText>

            <label
              className="usa-label"
              htmlFor={selectId}
            >
              {t('applyForSebtAccordionLabelSelectNumberPeople')}
            </label>
            <select
              id={selectId}
              className="usa-select"
              value={householdSize}
              onChange={(e) => setHouseholdSize(Number(e.target.value))}
              data-testid="household-size"
            >
              {sizes.map((size) => (
                <option
                  key={size}
                  value={size}
                >
                  {size}
                </option>
              ))}
            </select>

            <div className="usa-alert usa-alert--info margin-top-2">
              <div className="usa-alert__body">
                {/* aria-live so the recomputed threshold is announced on change,
                    rather than only being noticed by sighted users. */}
                <p
                  className="usa-alert__text"
                  data-testid="income-threshold"
                  aria-live="polite"
                >
                  {withThreshold(t('applyForSebtAccordionBodyAlertIncome'), threshold)}
                </p>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
