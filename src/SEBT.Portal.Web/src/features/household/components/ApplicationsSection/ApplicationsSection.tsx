'use client'

import { useTranslation } from 'react-i18next'

import type { Application } from '../../api'
import { useRequiredHouseholdData } from '../../api'

function getStatusTextClass(status: string): string {
  switch (status) {
    case 'Approved':
      return 'text-green'
    case 'Denied':
      return 'text-red'
    case 'Cancelled':
      return 'text-base-dark'
    default:
      return 'text-gold'
  }
}

// Keys map to CSV: "S2 - Portal Dashboard - Applications Table - {Key}"
// TODO: Add to CSV: "S2 - Portal Dashboard - Applications Table - Status Denied" for denied status
// TODO: Add to CSV: "S2 - Portal Dashboard - Applications Table - Status Pending" for pending status
// TODO: Add to CSV: "S2 - Portal Dashboard - Applications Table - Status Under Review" for under review status
// TODO: Add to CSV: "S2 - Portal Dashboard - Applications Table - Status Cancelled" for cancelled status

// Map application statuses to their display labels
// Only Approved is in the locale file currently
function getStatusLabel(status: string): string {
  // Return the status as-is - translations will be added to CSV later
  return status
}

function ApplicationCard({ application }: { application: Application }) {
  const { t } = useTranslation('dashboard')

  const childrenNames = application.children
    .map((child) => `${child.firstName} ${child.lastName}`)
    .join(', ')

  return (
    <div className="usa-card__container margin-bottom-2">
      <div className="usa-card__body">
        <dl className="margin-0">
          {application.caseNumber && (
            <>
              <dt className="text-bold">{t('applicationsTableHeadingNumber')}</dt>
              <dd className="margin-left-0 margin-bottom-2">{application.caseNumber}</dd>
            </>
          )}

          {application.children.length > 0 && (
            <>
              <dt className="text-bold">{t('applicationsTableHeadingChildrenIncluded')}</dt>
              <dd className="margin-left-0 margin-bottom-2">{childrenNames}</dd>
            </>
          )}

          <dt className="text-bold">{t('applicationsTableHeadingStatus')}</dt>
          <dd className="margin-left-0">
            <span className={`text-bold ${getStatusTextClass(application.applicationStatus)}`}>
              {getStatusLabel(application.applicationStatus)}
            </span>
          </dd>
        </dl>
      </div>
    </div>
  )
}

// Keys map to CSV: "S2 - Portal Dashboard - Section Applications - {Key}"
export function ApplicationsSection() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()

  if (data.applications.length === 0) {
    return null
  }

  return (
    <section
      aria-labelledby="applications-heading"
      className="margin-top-4"
    >
      <h2
        id="applications-heading"
        className="font-heading-lg margin-bottom-1"
      >
        {t('sectionApplicationsHeading')}
      </h2>
      <p className="margin-bottom-3">{t('sectionApplicationsBody')}</p>

      {data.applications.map((application, index) => (
        <ApplicationCard
          key={application.applicationNumber || `app-${index}`}
          application={application}
        />
      ))}
    </section>
  )
}
