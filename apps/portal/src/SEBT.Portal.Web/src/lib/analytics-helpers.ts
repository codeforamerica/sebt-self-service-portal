import { ApiError } from '@/api/client'
import type { AddressUpdateResponse } from '@/features/address/api/schema'
import type { HouseholdData } from '@/features/household'
import { getColoadingStatus } from '@/lib/coloadingStatus'
import { AnalyticsEvents } from '@sebt/analytics'
import type { StateCode } from '@sebt/design-system'

export const ANALYTICS_SCOPE: string[] = ['default', 'analytics']

type AddressUpdateAnalyticsStatus = 'success' | 'suggestion' | 'validation_error' | 'error'

type CardReplacementAnalyticsStatus = 'success' | 'error'

/** Whether a submitted address's state matches the portal's deployment (home) state. */
export type AddressStateCategory = 'home_state' | 'out_of_state'

interface DataLayerTrackFns {
  setPageData: (path: string, value: unknown, scope?: string | string[]) => void
  setUserData: (path: string, value: unknown, scope?: string | string[]) => void
  trackEvent: (name: string, data?: Record<string, unknown>) => void
}

/** Maps API failures to the shared analytics error_code taxonomy (see ADR 0015). */
export function apiErrorCodeFromUnknown(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403) return 'AUTH_FAILURE'
    if (error.status === 404) return 'NOT_FOUND'
    if (error.status === 429) return 'RATE_LIMIT'
    if (error.status >= 400 && error.status < 500) return 'INVALID_INPUT'
  }
  return 'TECH_ERROR'
}

export function syncColoadingStatus(
  setUserData: DataLayerTrackFns['setUserData'],
  isCoLoaded: boolean | null | undefined,
  household: Pick<HouseholdData, 'summerEbtCases' | 'applications'>
): void {
  setUserData('coloading_status', getColoadingStatus(isCoLoaded, household), ANALYTICS_SCOPE)
}

function addressUpdateStatusFromResult(
  result: AddressUpdateResponse
): AddressUpdateAnalyticsStatus {
  if (result.status === 'valid') return 'success'
  if (result.status === 'suggestion') return 'suggestion'
  return 'validation_error'
}

function addressUpdateErrorCodeFromResult(result: AddressUpdateResponse): string | null {
  if (result.status === 'valid') return null
  if (result.reason) return result.reason.toUpperCase()
  return result.status.toUpperCase()
}

/**
 * Classifies a submitted address by whether its state matches the portal's deployment (home)
 * state, for page.address_state_category on address_update_submit events. Compares
 * case-insensitively (submitted USPS code vs the deployment state code), so a DC deployment
 * treats "DC" as home_state and "VA" as out_of_state. An absent state classifies as
 * out_of_state. The submitted state stays a plain string because it is user-entered and can
 * be any US state, unlike the deployment's StateCode.
 */
export function classifyAddressState(
  submittedState: string | null | undefined,
  homeState: StateCode
): AddressStateCategory {
  return submittedState?.trim().toUpperCase() === homeState.toUpperCase()
    ? 'home_state'
    : 'out_of_state'
}

/** Emits address_update_submit; emits address_update_error when the API call fails. */
export function trackAddressUpdateSubmit(
  dl: Pick<DataLayerTrackFns, 'setPageData' | 'trackEvent'>,
  result: AddressUpdateResponse | null,
  error: unknown | null,
  addressStateCategory: AddressStateCategory | null = null
): void {
  if (addressStateCategory != null) {
    dl.setPageData('address_state_category', addressStateCategory)
  }

  if (error != null) {
    dl.setPageData('address_update_status', 'error')
    dl.setPageData('error_code', apiErrorCodeFromUnknown(error))
    dl.trackEvent(AnalyticsEvents.ADDRESS_UPDATE_SUBMIT)
    dl.trackEvent(AnalyticsEvents.ADDRESS_UPDATE_ERROR)
    return
  }

  if (result == null) {
    return
  }

  dl.setPageData('address_update_status', addressUpdateStatusFromResult(result))
  dl.setPageData('error_code', addressUpdateErrorCodeFromResult(result))
  dl.trackEvent(AnalyticsEvents.ADDRESS_UPDATE_SUBMIT)
}

/**
 * Emits address_update_validation_error for a client-side validation failure that blocks the
 * submit before any backend request. Sets `error_code` (taxonomy-aligned, e.g. TOO_LONG) and
 * `field_name` (the field that failed, e.g. streetAddress1); `flow`/`step` ride along from page
 * context.
 */
export function trackAddressUpdateValidationError(
  dl: Pick<DataLayerTrackFns, 'setPageData' | 'trackEvent'>,
  errorCode: string,
  fieldName: string
): void {
  dl.setPageData('error_code', errorCode)
  dl.setPageData('field_name', fieldName)
  dl.trackEvent(AnalyticsEvents.ADDRESS_UPDATE_VALIDATION_ERROR)
}

/** Emits card_replacement_submit; emits card_replacement_error when the API call fails. */
export function trackCardReplacementSubmit(
  dl: Pick<DataLayerTrackFns, 'setPageData' | 'trackEvent'>,
  error: unknown | null
): void {
  const status: CardReplacementAnalyticsStatus = error ? 'error' : 'success'
  const errorCode = error ? apiErrorCodeFromUnknown(error) : null

  dl.setPageData('card_replacement_status', status)
  dl.setPageData('error_code', errorCode)
  dl.trackEvent(AnalyticsEvents.CARD_REPLACEMENT_SUBMIT)

  if (error) {
    dl.trackEvent(AnalyticsEvents.CARD_REPLACEMENT_ERROR)
  }
}
