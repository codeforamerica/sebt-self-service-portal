'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'

import type { Address, HouseholdData } from '../../api'
import { formatUsPhone, useRequiredHouseholdData } from '../../api'

function formatAddress(address: Address): string {
  const parts = [
    address.streetAddress1,
    address.streetAddress2,
    [address.city, address.state, address.postalCode].filter(Boolean).join(', ')
  ].filter(Boolean)

  return parts.join('\n')
}

type StatusInfo = {
  labelKey: string
  variant: 'success' | 'warning' | 'error' | 'info'
}

// Keys map to CSV: "S2 - Portal Dashboard - Profile Table - Status {Status}"
const ENROLLED: StatusInfo = { labelKey: 'profileTableStatusEnrolled', variant: 'success' }
const APPLICATION_APPROVED: StatusInfo = {
  labelKey: 'profileTableStatusApplicationApproved',
  variant: 'success'
}
const APPLICATION_DENIED: StatusInfo = {
  labelKey: 'profileTableStatusApplicationDenied',
  variant: 'error'
}
const APPLICATION_IN_PROGRESS: StatusInfo = {
  labelKey: 'profileTableStatusApplicationIn-progress',
  variant: 'warning'
}
const APPLICATION_CANCELLED: StatusInfo = {
  labelKey: 'profileTableStatusCancelled',
  variant: 'info'
}
// Safe default when a connector reports a status the portal does not recognize. Distinct from
// APPLICATION_APPROVED, which is a known state that simply has no issued case yet.
const STATUS_UNAVAILABLE: StatusInfo = { labelKey: 'profileTableStatusUnknown', variant: 'info' }

/**
 * Every status label this component can render. Exported so statusLabelCoverage.test.ts can
 * assert each one resolves in every shipped locale. Keep in step with the constants above.
 */
export const PROFILE_STATUS_LABEL_KEYS = [
  ENROLLED,
  APPLICATION_APPROVED,
  APPLICATION_DENIED,
  APPLICATION_IN_PROGRESS,
  APPLICATION_CANCELLED,
  STATUS_UNAVAILABLE
].map((status) => status.labelKey)

function getApplicationStatus(data: HouseholdData): StatusInfo | null {
  const statuses = data.applications.map((app) => app.applicationStatus)
  if (statuses.length === 0) return null

  if (statuses.includes('Denied')) return APPLICATION_DENIED
  if (statuses.includes('Pending') || statuses.includes('UnderReview')) {
    return APPLICATION_IN_PROGRESS
  }
  if (statuses.includes('Cancelled')) return APPLICATION_CANCELLED
  if (statuses.every((s) => s === 'Approved')) return APPLICATION_APPROVED
  return STATUS_UNAVAILABLE
}

function getOverallStatus(data: HouseholdData): {
  primary: StatusInfo
  secondary: StatusInfo | null
} {
  const hasEnrolledCases = data.summerEbtCases.length > 0
  const appStatus = getApplicationStatus(data)

  if (hasEnrolledCases) {
    // "Enrolled" already communicates approval, so an approved application adds nothing.
    return {
      primary: ENROLLED,
      secondary: appStatus === APPLICATION_APPROVED ? null : appStatus
    }
  }

  if (appStatus) {
    return { primary: appStatus, secondary: null }
  }

  // No cases and no applications. DashboardContent renders EmptyState before reaching here.
  return { primary: STATUS_UNAVAILABLE, secondary: null }
}

function getStatusTextClass(variant: string): string {
  switch (variant) {
    case 'success':
      return 'text-green'
    case 'error':
      return 'text-red'
    case 'warning':
      return 'text-green'
    default:
      return 'text-base-dark'
  }
}

// Keys map to CSV: "S2 - Portal Dashboard - Profile Table - {Key}"
export function HouseholdSummary() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()
  const { primary, secondary } = getOverallStatus(data)
  const canUpdateAddress = data.allowedActions?.canUpdateAddress ?? true
  const showContactPreferences = useFeatureFlag('show_contact_preferences')

  return (
    <div className="usa-card__container margin-bottom-4">
      <div className="usa-card__body">
        <dl className="margin-0">
          {/* Status */}
          <dt className="text-bold">{t('profileTableHeadingStatus')}</dt>
          <dd className="margin-left-0 margin-bottom-2">
            <span className={`text-bold ${getStatusTextClass(primary.variant)}`}>
              {t(primary.labelKey)}
            </span>
            {secondary && (
              <>
                <span className="text-base-dark">{' / '}</span>
                <span className={`text-bold ${getStatusTextClass(secondary.variant)}`}>
                  {t(secondary.labelKey)}
                </span>
              </>
            )}
            {primary.variant === 'success' && (
              <p className="margin-top-1 margin-bottom-0">
                {t('profileTableStatusEnrolledDescription')}
              </p>
            )}
          </dd>

          {/* Your mailing address */}
          <dt className="text-bold">{t('profileTableHeadingAddress')}</dt>
          <dd className="margin-left-0 margin-bottom-2">
            <span style={{ whiteSpace: 'pre-line' }}>
              {data.addressOnFile ? formatAddress(data.addressOnFile) : '—'}
            </span>
            <br />
            {canUpdateAddress ? (
              <Link
                href="/profile/address"
                data-analytics-cta="update_address_cta"
                className="usa-link margin-top-1"
              >
                {t('profileTableActionChangeAddress')}
              </Link>
            ) : (
              <Link
                href="/profile/address/info"
                data-analytics-cta="update_address_info_cta"
                className="usa-link display-inline-block margin-top-1"
              >
                {/* TODO: design to add copy for if not editable and not co-loaded */}
                {t('profileTableCo-loadedAddress', '')}
              </Link>
            )}
          </dd>

          {/* Your preferred contact */}
          {showContactPreferences && (data.email || data.phone) && (
            <>
              <dt className="text-bold">{t('profileTableHeadingContact')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                {data.email}
                {data.phone && (
                  <>
                    {data.email && <br />}
                    {formatUsPhone(data.phone)}
                  </>
                )}
                <br />
                <Link
                  href="/contact"
                  data-analytics-cta="update_contact_cta"
                  className="usa-link"
                >
                  {t('profileTableActionChangeContact')}
                </Link>
              </dd>
            </>
          )}
        </dl>
      </div>
    </div>
  )
}
