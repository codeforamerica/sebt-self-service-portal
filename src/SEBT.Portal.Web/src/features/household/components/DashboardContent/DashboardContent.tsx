'use client'

import { ApiError } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { SignOutLink, useAuth } from '@/features/auth'
import { getColoadingStatus } from '@/lib/coloadingStatus'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, getState } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { useHouseholdData } from '../../api'
import { ActionButtons } from '../ActionButtons'
import { ApplicationsSection } from '../ApplicationsSection'
import { DashboardAlerts } from '../DashboardAlerts'
import { DashboardSkeleton } from '../DashboardSkeleton'
import { EbtEdgeSection } from '../EbtEdgeSection'
import { EmptyState } from '../EmptyState'
import { EnrolledChildren } from '../EnrolledChildren'
import { HouseholdSummary } from '../HouseholdSummary'
import { UserProfileCard } from '../UserProfileCard'

// TODO: Add to CSV: "S2 - Portal Dashboard - Error Heading" and "S2 - Portal Dashboard - Error Description"
export function DashboardContent() {
  const { t } = useTranslation('dashboard')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const { data, isLoading, isError, error, requiresProofing } = useHouseholdData()
  const { setPageData, setUserData, trackEvent } = useDataLayer()
  const { session } = useAuth()
  const sessionIsCoLoaded = session?.isCoLoaded
  const isCO = getState() === 'co'

  useEffect(() => {
    if (isLoading) return
    if (isError) {
      setPageData('household_status', 'error')
    } else if (data) {
      const childCount = data.summerEbtCases.length
      const isEmpty = childCount === 0 && data.applications.length === 0
      setPageData('household_status', isEmpty ? 'empty' : 'success')
      setUserData('household_linked_children', childCount, ['default', 'analytics'])

      // DC-215: classify the household into one of four buckets so analytics can
      // segment dashboard usage. Same value is mirrored on user.* (persists across
      // pages) and page.* (lives with this page_load / household_result event).
      // Passing the raw nullable claim means an unresolved auth state tags
      // `unknown` instead of biasing toward `non_co_loaded`.
      const coloadingStatus = getColoadingStatus(sessionIsCoLoaded, data)
      setUserData('coloading_status', coloadingStatus, ['default', 'analytics'])
      setPageData('household_type', coloadingStatus)

      // Distinguishes a co-loaded user who matched but has no enrolled children
      // from a non-co-loaded applicant seeing the same empty screen. Only fires
      // for a definitively true claim — null/undefined auth shouldn't infer it.
      if (isEmpty && sessionIsCoLoaded === true) {
        setPageData('household_reason', 'no_children')
      }
    }
    trackEvent(AnalyticsEvents.HOUSEHOLD_RESULT)
  }, [isLoading, isError, data, sessionIsCoLoaded, setPageData, setUserData, trackEvent])

  // Visually hidden h1 for accessibility - provides page structure for screen readers
  const pageHeading = <h1 className="usa-sr-only">{t('pageTitle', 'SUN Bucks Dashboard')}</h1>

  if (isLoading || requiresProofing) {
    if (isCO) {
      // CoLoadingScreen renders its own h1 ("Please wait..."), so omit pageHeading
      // here to avoid two h1 elements on the same view.
      return (
        <CoLoadingScreen
          title={tProcessing('title', 'Please wait...')}
          message={tProcessing(
            'body',
            'Do not exit the page. Checking to see if we have enough information.'
          )}
        />
      )
    }
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
        <SignOutLink />
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

  if (!data || isNotFound || (data.summerEbtCases.length === 0 && data.applications.length === 0)) {
    return (
      <>
        {pageHeading}
        {data?.userProfile ? <UserProfileCard /> : <SignOutLink />}
        <EmptyState />
      </>
    )
  }

  return (
    <>
      {pageHeading}
      <DashboardAlerts />
      <ActionButtons allowedActions={data.allowedActions} />
      <UserProfileCard />
      <HouseholdSummary />
      <EnrolledChildren />
      <EbtEdgeSection />
      <ApplicationsSection />
    </>
  )
}
