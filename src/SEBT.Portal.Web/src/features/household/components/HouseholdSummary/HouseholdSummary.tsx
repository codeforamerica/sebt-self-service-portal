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
  fallback: string
  variant: 'success' | 'warning' | 'error' | 'info'
}

// Keys map to CSV: "S2 - Portal Dashboard - Profile Table - Status {Status}"
function getApplicationStatus(data: HouseholdData): StatusInfo | null {
  const statuses = data.applications.map((app) => app.applicationStatus)
  if (statuses.length === 0) return null

  // If all applications are approved, there's no distinct application status to show
  if (statuses.every((s) => s === 'Approved')) return null

  if (statuses.includes('Denied')) {
    return {
      labelKey: 'profileTableStatusApplicationDenied',
      fallback: 'Application denied',
      variant: 'error'
    }
  }
  if (statuses.includes('Pending') || statuses.includes('UnderReview')) {
    return {
      labelKey: 'profileTableStatusApplicationIn-progress',
      fallback: 'Application in-process',
      variant: 'warning'
    }
  }
  if (statuses.includes('Cancelled')) {
    // TODO: Add CSV row "S2 - Portal Dashboard - Profile Table - Status Cancelled"
    return { labelKey: 'profileTableStatusCancelled', fallback: 'Cancelled', variant: 'info' }
  }
  // TODO: Add CSV row "S2 - Portal Dashboard - Profile Table - Status Unknown"
  return { labelKey: 'profileTableStatusUnknown', fallback: 'Unknown', variant: 'info' }
}

function getOverallStatus(data: HouseholdData): {
  primary: StatusInfo
  secondary: StatusInfo | null
} {
  const hasEnrolledCases = data.summerEbtCases.length > 0
  const appStatus = getApplicationStatus(data)

  if (hasEnrolledCases) {
    return {
      primary: { labelKey: 'profileTableStatusEnrolled', fallback: 'Enrolled', variant: 'success' },
      secondary: appStatus
    }
  }

  if (appStatus) {
    return { primary: appStatus, secondary: null }
  }

  return {
    primary: { labelKey: 'profileTableStatusUnknown', fallback: 'Unknown', variant: 'info' },
    secondary: null
  }
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
              {t(primary.labelKey, primary.fallback)}
            </span>
            {secondary && (
              <>
                <span className="text-base-dark">{' / '}</span>
                <span className={`text-bold ${getStatusTextClass(secondary.variant)}`}>
                  {t(secondary.labelKey, secondary.fallback)}
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
