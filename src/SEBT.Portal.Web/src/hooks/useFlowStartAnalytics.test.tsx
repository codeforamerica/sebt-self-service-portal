import { AnalyticsEvents } from '@sebt/analytics'
import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'
import { syncColoadingStatus } from '@/lib/analytics-helpers'

import { useFlowStartAnalytics } from './useFlowStartAnalytics'

const mockSetUserData = vi.fn()
const mockTrackEvent = vi.fn()

vi.mock('@sebt/analytics', () => ({
  AnalyticsEvents: {
    ADDRESS_UPDATE_START: 'address_update_start',
    CARD_REPLACEMENT_START: 'card_replacement_start'
  },
  useDataLayer: () => ({
    setUserData: mockSetUserData,
    trackEvent: mockTrackEvent
  })
}))

const mockUseAuth = vi.fn()
vi.mock('@/features/auth', () => ({
  useAuth: () => mockUseAuth()
}))

const mockUseHouseholdData = vi.fn()
vi.mock('@/features/household', () => ({
  useHouseholdData: () => mockUseHouseholdData()
}))

vi.mock('@/lib/analytics-helpers', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/analytics-helpers')>()
  return {
    ...actual,
    syncColoadingStatus: vi.fn()
  }
})

const mockHouseholdData = {
  summerEbtCases: [],
  applications: []
} as Pick<HouseholdData, 'summerEbtCases' | 'applications'>

describe('useFlowStartAnalytics', () => {
  let rafCallback: FrameRequestCallback | null = null
  const rafId = 42

  beforeEach(() => {
    mockSetUserData.mockClear()
    mockTrackEvent.mockClear()
    vi.mocked(syncColoadingStatus).mockClear()
    rafCallback = null

    mockUseAuth.mockReturnValue({
      session: { isCoLoaded: true }
    })
    mockUseHouseholdData.mockReturnValue({ data: mockHouseholdData })

    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((cb) => {
      rafCallback = cb
      return rafId
    })
    vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(vi.fn())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  function flushAnimationFrame() {
    act(() => {
      rafCallback?.(0)
    })
  }

  it('fires once when enabled and household data are present', () => {
    renderHook(() => useFlowStartAnalytics(AnalyticsEvents.ADDRESS_UPDATE_START, true))

    flushAnimationFrame()

    expect(vi.mocked(syncColoadingStatus)).toHaveBeenCalledWith(
      mockSetUserData,
      true,
      mockHouseholdData
    )
    expect(mockTrackEvent).toHaveBeenCalledTimes(1)
    expect(mockTrackEvent).toHaveBeenCalledWith(AnalyticsEvents.ADDRESS_UPDATE_START)
  })

  it('does not fire when disabled', () => {
    renderHook(() => useFlowStartAnalytics(AnalyticsEvents.CARD_REPLACEMENT_START, false))

    flushAnimationFrame()

    expect(vi.mocked(syncColoadingStatus)).not.toHaveBeenCalled()
    expect(mockTrackEvent).not.toHaveBeenCalled()
  })

  it('does not fire when household data is null', () => {
    mockUseHouseholdData.mockReturnValue({ data: undefined })

    renderHook(() => useFlowStartAnalytics(AnalyticsEvents.ADDRESS_UPDATE_START, true))

    flushAnimationFrame()

    expect(vi.mocked(syncColoadingStatus)).not.toHaveBeenCalled()
    expect(mockTrackEvent).not.toHaveBeenCalled()
  })

  it('does not double-fire across re-renders', () => {
    const { rerender } = renderHook(
      ({ enabled }) => useFlowStartAnalytics(AnalyticsEvents.ADDRESS_UPDATE_START, enabled),
      { initialProps: { enabled: true } }
    )

    flushAnimationFrame()

    rerender({ enabled: true })
    flushAnimationFrame()

    expect(mockTrackEvent).toHaveBeenCalledTimes(1)
  })

  it('cancels the pending animation frame on unmount', () => {
    const { unmount } = renderHook(() =>
      useFlowStartAnalytics(AnalyticsEvents.ADDRESS_UPDATE_START, true)
    )

    unmount()

    expect(window.cancelAnimationFrame).toHaveBeenCalledWith(rafId)
    expect(mockTrackEvent).not.toHaveBeenCalled()
  })
})
