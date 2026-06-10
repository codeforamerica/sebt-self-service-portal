'use client'

import { getEnrollmentConfig } from '@/lib/stateConfig'
import { Alert } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useCheckerFeatures } from '../hooks/useCheckerFeatures'
import { resolveMaintenanceMessage } from '../resolveMaintenanceMessage'

export function MaintenanceBanner() {
  // useTranslation subscribes this component to language changes, so the banner
  // re-resolves its configured copy when the user switches languages.
  const { i18n } = useTranslation()
  const { apiBaseUrl } = getEnrollmentConfig()
  const { data, error } = useCheckerFeatures(apiBaseUrl)

  useEffect(() => {
    if (error) {
      // Fail closed (no banner) so a config outage doesn't look like planned
      // maintenance, but keep the failure observable.
      console.warn('Maintenance banner state unavailable; hiding banner.', error)
    }
  }, [error])

  if (!data) {
    return null
  }

  const message = resolveMaintenanceMessage(
    data.maintenanceBanner.enabled,
    data.maintenanceBanner.message,
    i18n.language
  )

  if (!message) {
    return null
  }

  return (
    <Alert
      variant="warning"
      className="margin-top-0"
    >
      {message}
    </Alert>
  )
}
