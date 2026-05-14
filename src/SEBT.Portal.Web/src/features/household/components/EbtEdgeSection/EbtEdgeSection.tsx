'use client'

import { useState } from 'react'
import { useTranslation } from 'react-i18next'

// Keys map to CSV: "S2 - Portal Dashboard - Alert EBT Edge - {Key}"
export function EbtEdgeSection() {
  const { t } = useTranslation('dashboard')
  const [isExpanded, setIsExpanded] = useState(false)

  const handleToggle = () => {
    setIsExpanded((prev) => !prev)
  }

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
      <div className="usa-accordion">
        <h3 className="usa-accordion__heading">
          <button
            type="button"
            className="usa-accordion__button bg-transparent border-0 padding-y-2 padding-x-0"
            aria-expanded={isExpanded}
            aria-controls="help-content"
            onClick={handleToggle}
          >
            <span className="display-flex flex-align-center text-info-darker">
              <svg
                className="usa-icon margin-right-1"
                aria-hidden="true"
                focusable="false"
                role="img"
              >
                <use xlinkHref="/img/sprite.svg#info" />
              </svg>
              {t('alertEbtEdgeTitle')}
            </span>
          </button>
        </h3>
        <div
          id="help-content"
          className="usa-accordion__content usa-prose"
          hidden={!isExpanded}
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
        </div>
      </div>
    </section>
  )
}
