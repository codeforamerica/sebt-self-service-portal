'use client'

import { useTranslation } from 'react-i18next'

import { useRequiredHouseholdData } from '../../api'
import { ChildCard } from '../ChildCard'

// Keys map to CSV: "S2 - Portal Dashboard - Section Enrolled Children - {Key}"
export function EnrolledChildren() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()

  // Flatten children across all applications for display
  // Each child gets the application-level data it belongs to
  let childIndex = 0
  const childrenWithApplicationData = data.applications.flatMap((application) =>
    application.children.map((child) => ({
      child,
      application,
      index: childIndex++
    }))
  )

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
        {childrenWithApplicationData.map(({ child, application, index }) => (
          <ChildCard
            key={`${child.firstName}-${child.lastName}-${index}`}
            child={child}
            application={application}
            id={`${index}`}
            defaultExpanded={index === 0}
          />
        ))}
      </div>
    </section>
  )
}
