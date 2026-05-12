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
import { toAnalyticsCohort } from '../../api/schema'
import { ActionButtons } from '../ActionButtons'
import { ApplicationsSection } from '../ApplicationsSection'
import { DashboardAlerts } from '../DashboardAlerts'
import { DashboardSkeleton } from '../DashboardSkeleton'
import { EbtEdgeSection } from '../EbtEdgeSection'
import { EmptyState } from '../EmptyState'
import { EnrolledChildren } from '../EnrolledChildren'
import { HouseholdSummary } from '../HouseholdSummary'
import { UserProfileCard } from '../UserProfileCard'

/**
 * Closed taxonomy of dashboard error codes per docs/adr/0015-co-loaded-error-code-taxonomy.md.
 * Adding a new value requires an ADR amendment plus matching updates in the
 * Serilog `OutcomeCode` field on the backend and in the i18n locale keys.
 */
export type DashboardErrorCode =
  | 'NOT_FOUND'
  | 'NO_CHILDREN'
  | 'AUTH_FAILURE'
  | 'TECH_ERROR'
  | 'INVALID_INPUT'

// Maps an HTTP failure to one of the analytics taxonomy buckets. NOT_FOUND
// covers 404 (the connector returned no household for the user); 4xx other
// than 404 are treated as auth/permission failures (the API redirects 401/403
// before reaching the dashboard, but tag them here defensively); everything
// else lands in TECH_ERROR. INVALID_INPUT is reserved for form-submission
// 400s with ValidationProblemDetails — the dashboard doesn't submit forms,
// so it is intentionally never produced here.
function dashboardErrorCodeFromStatus(error: unknown): DashboardErrorCode {
  if (!(error instanceof ApiError)) return 'TECH_ERROR'
  if (error.status === 404) return 'NOT_FOUND'
  if (error.status === 401 || error.status === 403) return 'AUTH_FAILURE'
  return 'TECH_ERROR'
}

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
    // 403 with requiredIal is a redirect to ID proofing, not a dashboard error.
    // Skip analytics emission so the household_result event isn't tagged as
    // an AUTH_FAILURE for what is really a step-up flow.
    if (requiresProofing) return
    if (isError) {
      setPageData('household_status', 'error')
      setPageData('error_code', dashboardErrorCodeFromStatus(error))
    } else if (data) {
      const childCount = data.summerEbtCases.length
      const isEmpty = childCount === 0 && data.applications.length === 0
      setPageData('household_status', isEmpty ? 'empty' : 'success')
      // Reset error_code so a stale value from a prior render (e.g. the user
      // refreshed off an error state into success) does not persist on the
      // next household_result event.
      setPageData('error_code', null)
      setUserData('household_linked_children', childCount, ['default', 'analytics'])
      setUserData('co_loaded_cohort', toAnalyticsCohort(data.coLoadedCohort), [
        'default',
        'analytics'
      ])

      // Classify the household into one of four buckets so analytics can
      // segment dashboard usage. Same value is mirrored on user.* (persists
      // across pages) and page.* (lives with this page_load /
      // household_result event). Passing the raw nullable claim means an
      // unresolved auth state tags `unknown` instead of biasing toward
      // `non_co_loaded`.
      const coloadingStatus = getColoadingStatus(sessionIsCoLoaded, data)
      setUserData('coloading_status', coloadingStatus, ['default', 'analytics'])
      setPageData('household_type', coloadingStatus)
      // An empty household is a separate analytics failure category from
      // server errors — surface it as error_code='NO_CHILDREN' so dashboards
      // can split "couldn't reach data" from "got data, none qualifies".
      if (isEmpty) {
        setPageData('error_code', 'NO_CHILDREN')
        // Distinguishes a co-loaded user who matched but has no enrolled
        // children from a non-co-loaded applicant seeing the same empty
        // screen. Only fires for a definitively true claim — null/undefined
        // auth shouldn't infer it.
        if (sessionIsCoLoaded === true) {
          setPageData('household_reason', 'no_children')
        }
      }

      // hashedAppId is gated server-side (CO only); skip the call when absent.
      if (data.hashedAppId) {
        setUserData('hashed_app_id', data.hashedAppId, ['default', 'analytics'])
      }
    }
    trackEvent(AnalyticsEvents.HOUSEHOLD_RESULT)
  }, [
    isLoading,
    isError,
    error,
    data,
    requiresProofing,
    sessionIsCoLoaded,
    setPageData,
    setUserData,
    trackEvent
  ])

  // Visually hidden h1 for accessibility - provides page structure for screen readers
  const pageHeading = <h1 className="usa-sr-only">{t('pageTitle', 'SUN Bucks Dashboard')}</h1>

  if (isLoading || requiresProofing) {
    if (isCO) {
      // CoLoadingScreen renders its own h1 ("Please wait..."), so omit pageHeading
      // here to avoid two h1 elements on the same view.
      return (
        <CoLoadingScreen
          title={tProcessing('title')}
          message={tProcessing('body')}
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
      {data.userProfile ? <UserProfileCard /> : <SignOutLink />}
      <HouseholdSummary />
      <EnrolledChildren />
      <EbtEdgeSection />
      <ApplicationsSection />
    </>
  )
}
