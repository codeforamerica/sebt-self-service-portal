'use client'

import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'

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
      return 'text-green'
  }
}

// Keys map to CSV: "S2 - Portal Dashboard - Applications Table - Status {Status}"
// TODO: Add CSV rows for: Status Denied, Status Pending, Status Under Review, Status Cancelled
const APPLICATION_STATUS_KEYS: Record<string, { key: string; fallback: string }> = {
  Approved: { key: 'applicationsTableStatusApproved', fallback: 'Approved' },
  Denied: { key: 'applicationsTableStatusDenied', fallback: 'Denied' },
  Pending: { key: 'applicationsTableStatusPending', fallback: 'Pending' },
  // TODO update
  UnderReview: { key: 'applicationsTableStatusUnderReview', fallback: 'Under Review' },
  // TODO update

  Cancelled: { key: 'applicationsTableStatusCancelled', fallback: 'Cancelled' }
}

function ApplicationCard({ application }: { application: Application }) {
  const { t } = useTranslation('dashboard')
  const showCaseNumber = useFeatureFlag('show_case_number')

  const childrenNames = application.children
    .map((child) => `${child.firstName} ${child.lastName}`)
    .join(', ')

  return (
    <div className="usa-card__container margin-bottom-2">
      <div className="usa-card__body">
        <dl className="margin-0">
          {showCaseNumber && application.caseNumber && (
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
              {(() => {
                const entry = APPLICATION_STATUS_KEYS[application.applicationStatus]
                return entry ? t(entry.key, entry.fallback) : application.applicationStatus
              })()}
            </span>
          </dd>
        </dl>
      </div>
    </div>
  )
}

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
