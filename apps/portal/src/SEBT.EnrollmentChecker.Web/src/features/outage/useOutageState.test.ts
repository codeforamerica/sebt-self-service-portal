import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { CheckerFeatures } from '../maintenance/api/fetchCheckerFeatures'
import { useCheckerFeatures } from '../maintenance/hooks/useCheckerFeatures'
import { useOutageState } from './useOutageState'

vi.mock('../maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: vi.fn()
}))

const mockUseCheckerFeatures = vi.mocked(useCheckerFeatures)

function arrange(state: {
  data?: CheckerFeatures
  isPending?: boolean
  error?: Error
  isStale?: boolean
}) {
  mockUseCheckerFeatures.mockReturnValue({
    data: state.data,
    isPending: state.isPending ?? false,
    error: state.error ?? null,
    isStale: state.isStale ?? false
  } as unknown as ReturnType<typeof useCheckerFeatures>)
}

function features(outagePage?: { enabled: boolean }): CheckerFeatures {
  return {
    maintenanceBanner: { enabled: false, message: {} },
    ...(outagePage ? { outagePage } : {})
  }
}

describe('useOutageState', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('is inactive and pending before the first successful fetch', () => {
    arrange({ isPending: true })

    const { result } = renderHook(() => useOutageState(''))

    expect(result.current.outageActive).toBe(false)
    expect(result.current.isPending).toBe(true)
  })

  it('is inactive when the API omits outagePage (older API version)', () => {
    arrange({ data: features() })

    expect(renderHook(() => useOutageState('')).result.current.outageActive).toBe(false)
  })

  it('is inactive when the API reports the outage page off', () => {
    arrange({ data: features({ enabled: false }) })

    expect(renderHook(() => useOutageState('')).result.current.outageActive).toBe(false)
  })

  it('is active when the API reports the outage page on', () => {
    arrange({ data: features({ enabled: true }) })

    expect(renderHook(() => useOutageState('')).result.current.outageActive).toBe(true)
  })

  it('stays active when polls fail after an active outage was seen (sticky)', () => {
    // React Query keeps the last successful payload in `data` across failed refetches;
    // the hook must not apply the banner's staleness veto here.
    arrange({
      data: features({ enabled: true }),
      error: new Error('poll failed'),
      isStale: true
    })

    expect(renderHook(() => useOutageState('')).result.current.outageActive).toBe(true)
  })

  it('stays inactive when polls fail and the last-known state was inactive (fail closed)', () => {
    arrange({
      data: features({ enabled: false }),
      error: new Error('poll failed'),
      isStale: true
    })

    expect(renderHook(() => useOutageState('')).result.current.outageActive).toBe(false)
  })
})
