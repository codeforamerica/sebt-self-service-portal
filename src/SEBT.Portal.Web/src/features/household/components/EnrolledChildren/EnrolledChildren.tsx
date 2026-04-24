'use client'

import { useTranslation } from 'react-i18next'

import { useRequiredHouseholdData } from '../../api'
import { ChildCard } from '../ChildCard'

// Keys map to CSV: "S2 - Portal Dashboard - Section Enrolled Children - {Key}"
export function EnrolledChildren() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()

  return (
    <section aria-labelledby="enrolled-children-heading">
      <h2
        id="enrolled-children-heading"
        className="font-heading-lg margin-bottom-1"
      >
        {t('sectionEnrolledChildrenHeading')}
      </h2>
      <p className="margin-bottom-3">
        {t('sectionEnrolledChildrenBody1')}{' '}
        <a
          href="/apply"
          className="usa-link"
        >
          {t('sectionEnrolledChildrenAction')}
        </a>
      </p>

      <div
        className="usa-accordion usa-accordion--bordered"
        data-allow-multiple
      >
        {data.summerEbtCases.map((c, index) => (
          <ChildCard
            key={`${c.childFirstName}-${c.childLastName}-${c.childDateOfBirth}-${c.summerEBTCaseID}`}
            summerEbtCase={c}
            defaultExpanded={index === 0}
            canRequestReplacementCard={data.allowedActions?.canRequestReplacementCard}
          />
        ))}
      </div>
    </section>
  )
}
