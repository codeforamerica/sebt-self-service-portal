'use client'

import { Details } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

// Keys map to CSV: "S2 - Portal Dashboard - Alert EBT Edge - {Key}"
export function EbtEdgeSection() {
  const { t } = useTranslation('dashboard')

  // Parse features string into array of bullet points
  const featuresText = t('alertEbtEdgeFeatures')
  const features = featuresText.split('\n').filter(Boolean)

  return (
    <section
      className="margin-top-4"
      aria-labelledby="help-section-heading"
    >
      {/* TODO: Add to CSV: "S2 - Portal Dashboard - Alert EBT Edge - Section Heading" */}
      <h2
        id="help-section-heading"
        className="usa-sr-only"
      >
        {t('alertEbtEdgeSectionHeading', 'EBT Card Help')}
      </h2>
      <Details
        headingLevel={3}
        summary={t('alertEbtEdgeTitle')}
        icon={
          <svg
            className="usa-icon margin-right-1"
            aria-hidden="true"
            focusable="false"
            role="img"
          >
            <use xlinkHref="/img/sprite.svg#info" />
          </svg>
        }
      >
        <p>{t('alertEbtEdgeBody')}</p>
        <ul className="usa-list margin-top-2">
          {features.map((feature, index) => (
            <li key={index}>{feature}</li>
          ))}
        </ul>
        <a
          href="https://www.ebtedge.com"
          className="usa-link text-bold"
          target="_blank"
          rel="noopener noreferrer"
        >
          {t('alertEbtEdgeAction')}
        </a>
      </Details>
    </section>
  )
}
