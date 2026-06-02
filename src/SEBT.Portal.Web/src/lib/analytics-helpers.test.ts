import { ApiError } from '@/api/client'
import type { AddressUpdateResponse } from '@/features/address/api/schema'
import { AnalyticsEvents } from '@sebt/analytics'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  apiErrorCodeFromUnknown,
  trackAddressUpdateSubmit,
  trackCardReplacementSubmit
} from './analytics-helpers'

describe('apiErrorCodeFromUnknown', () => {
  it('maps 401/403 to AUTH_FAILURE', () => {
    expect(apiErrorCodeFromUnknown(new ApiError('Unauthorized', 401))).toBe('AUTH_FAILURE')
    expect(apiErrorCodeFromUnknown(new ApiError('Forbidden', 403))).toBe('AUTH_FAILURE')
  })

  it('maps 404 to NOT_FOUND', () => {
    expect(apiErrorCodeFromUnknown(new ApiError('Not found', 404))).toBe('NOT_FOUND')
  })

  it('maps 429 to RATE_LIMIT', () => {
    expect(apiErrorCodeFromUnknown(new ApiError('Too many', 429))).toBe('RATE_LIMIT')
  })

  it('maps other 4xx to INVALID_INPUT', () => {
    expect(apiErrorCodeFromUnknown(new ApiError('Bad request', 400))).toBe('INVALID_INPUT')
  })

  it('maps unknown errors to TECH_ERROR', () => {
    expect(apiErrorCodeFromUnknown(new Error('network'))).toBe('TECH_ERROR')
  })
})

describe('trackAddressUpdateSubmit', () => {
  const setPageData = vi.fn()
  const trackEvent = vi.fn()

  beforeEach(() => {
    setPageData.mockClear()
    trackEvent.mockClear()
  })

  it('emits submit with success status when address is valid', () => {
    const result: AddressUpdateResponse = { status: 'valid' }

    trackAddressUpdateSubmit({ setPageData, trackEvent }, result, null)

    expect(setPageData).toHaveBeenCalledWith('address_update_status', 'success')
    expect(setPageData).toHaveBeenCalledWith('error_code', null)
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_SUBMIT)
    expect(trackEvent).not.toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_ERROR)
  })

  it('emits submit with suggestion status and reason error_code', () => {
    const result: AddressUpdateResponse = { status: 'suggestion', reason: 'abbreviated' }

    trackAddressUpdateSubmit({ setPageData, trackEvent }, result, null)

    expect(setPageData).toHaveBeenCalledWith('address_update_status', 'suggestion')
    expect(setPageData).toHaveBeenCalledWith('error_code', 'ABBREVIATED')
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_SUBMIT)
    expect(trackEvent).not.toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_ERROR)
  })

  it('emits submit and error events when the API call fails', () => {
    trackAddressUpdateSubmit({ setPageData, trackEvent }, null, new ApiError('Server error', 500))

    expect(setPageData).toHaveBeenCalledWith('address_update_status', 'error')
    expect(setPageData).toHaveBeenCalledWith('error_code', 'TECH_ERROR')
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_SUBMIT)
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_ERROR)
  })
})

describe('trackCardReplacementSubmit', () => {
  const setPageData = vi.fn()
  const trackEvent = vi.fn()

  beforeEach(() => {
    setPageData.mockClear()
    trackEvent.mockClear()
  })

  it('emits submit with success status on success', () => {
    trackCardReplacementSubmit({ setPageData, trackEvent }, null)

    expect(setPageData).toHaveBeenCalledWith('card_replacement_status', 'success')
    expect(setPageData).toHaveBeenCalledWith('error_code', null)
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_SUBMIT)
    expect(trackEvent).not.toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_ERROR)
  })

  it('emits submit and error events on failure', () => {
    trackCardReplacementSubmit({ setPageData, trackEvent }, new ApiError('Cooldown', 400))

    expect(setPageData).toHaveBeenCalledWith('card_replacement_status', 'error')
    expect(setPageData).toHaveBeenCalledWith('error_code', 'INVALID_INPUT')
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_SUBMIT)
    expect(trackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_ERROR)
  })
})
