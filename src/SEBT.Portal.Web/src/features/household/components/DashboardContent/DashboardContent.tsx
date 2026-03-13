'use client'

import { ApiError } from '@/api'
import { Alert } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

import { useHouseholdData } from '../../api'
import { ActionButtons } from '../ActionButtons'
import { ApplicationsSection } from '../ApplicationsSection'
import { DashboardSkeleton } from '../DashboardSkeleton'
import { EbtEdgeSection } from '../EbtEdgeSection'
import { EmptyState } from '../EmptyState'
import { EnrolledChildren } from '../EnrolledChildren'
import { HouseholdSummary } from '../HouseholdSummary'
import { UserProfileCard } from '../UserProfileCard'

// TODO: Add to CSV: "S2 - Portal Dashboard - Error Heading" and "S2 - Portal Dashboard - Error Description"
export function DashboardContent() {
  const { t } = useTranslation('dashboard')
  const { data, isLoading, isError, error } = useHouseholdData()

  // Visually hidden h1 for accessibility - provides page structure for screen readers
  const pageHeading = <h1 className="usa-sr-only">{t('pageTitle', 'SUN Bucks Dashboard')}</h1>

  if (isLoading) {
    return (
      <>
        {pageHeading}
        <DashboardSkeleton />
      </>
    )
  }

  // 404 means no household data found - show empty state instead of error
  const isNotFound = error instanceof ApiError && error.status === 404

  if (isError && !isNotFound) {
    return (
      <>
        {pageHeading}
        <Alert
          variant="error"
          heading={t('errorHeading', 'Error loading dashboard')}
        >
          {t(
            'errorDescription',
            'There was an error loading your dashboard. Please try again later.'
          )}
        </Alert>
      </>
    )
  }

  if (!data || data.applications.length === 0 || isNotFound) {
    return (
      <>
        {pageHeading}
        {data?.userProfile && <UserProfileCard />}
        <EmptyState />
      </>
    )
  }

  return (
    <>
      {pageHeading}
      <ActionButtons />
      <UserProfileCard />
      <HouseholdSummary />
      <EnrolledChildren />
      <EbtEdgeSection />
      <ApplicationsSection />
    </>
  )
}
