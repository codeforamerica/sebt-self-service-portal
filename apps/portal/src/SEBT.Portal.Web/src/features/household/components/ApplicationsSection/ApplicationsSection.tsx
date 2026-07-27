'use client'

import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'

import type { Application, ApplicationStatus } from '../../api'
import { formatDate, useRequiredHouseholdData } from '../../api'

function getStatusTextClass(status: ApplicationStatus): string {
  switch (status) {
    case 'Approved':
      return 'text-green'
    case 'Denied':
      return 'text-red'
    case 'Cancelled':
    case 'Unknown':
      return 'text-base-dark'
    default:
      return 'text-green'
  }
}

// Keys map to CSV: "S2 - Portal Dashboard - Applications Table - Status {Status}"
// Exhaustive by type, so a new ApplicationStatus cannot ship without a label.
// Exported so statusLabelCoverage.test.ts can assert every key resolves in every shipped locale.
export const APPLICATION_STATUS_KEYS: Record<ApplicationStatus, string> = {
  Approved: 'applicationsTableStatusApproved',
  Denied: 'applicationsTableStatusDenied',
  Pending: 'applicationsTableStatusPending',
  UnderReview: 'applicationsTableStatusUnderReview',
  Cancelled: 'applicationsTableStatusCancelled',
  // Safe default when a connector reports a status the portal does not recognize.
  Unknown: 'applicationsTableStatusUnknown'
}

function ApplicationCard({ application }: { application: Application }) {
  const { t, i18n } = useTranslation('dashboard')
  const showCaseNumber = useFeatureFlag('show_case_number')
  const showApplicationDate = useFeatureFlag('show_application_date')

  const childrenNames = application.children
    .map((child) => `${child.firstName} ${child.lastName}`)
    .join(', ')

  return (
    <div className="usa-card__container margin-bottom-2">
      <div className="usa-card__body">
        <dl className="margin-0">
          {showApplicationDate && application.applicationDate && (
            <>
              <dt className="text-bold">{t('applicationsTableHeadingDateSubmitted')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                {formatDate(application.applicationDate, i18n.language)}
              </dd>
            </>
          )}

          {/* States that omit the label in their content sheet do not surface a case number. */}
          {showCaseNumber &&
            application.caseNumber &&
            i18n.exists('dashboard:applicationsTableHeadingNumber') && (
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
              {t(APPLICATION_STATUS_KEYS[application.applicationStatus])}
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
