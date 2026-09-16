import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useApplyHref } from './useApplyHref'

const CONFIGURED_URL = 'https://apply.example.gov/'

let mockApplyHref: string | null = CONFIGURED_URL
let mockFeatures: unknown = { apply: { enabled: true } }

vi.mock('./applyHref', () => ({
  getApplyHref: () => mockApplyHref
}))

vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => ({ data: mockFeatures })
}))

describe('useApplyHref', () => {
  beforeEach(() => {
    mockApplyHref = CONFIGURED_URL
    mockFeatures = { apply: { enabled: true } }
  })

  it('returns the destination when the flag is on and a URL is configured', () => {
    expect(renderHook(() => useApplyHref()).result.current).toBe(CONFIGURED_URL)
  })

  // Both conditions have to hold; either one alone is not enough.
  it('returns null when the flag is off even though a URL is configured', () => {
    mockFeatures = { apply: { enabled: false } }
    expect(renderHook(() => useApplyHref()).result.current).toBeNull()
  })

  it('returns null when the flag is on but no URL is configured', () => {
    mockApplyHref = null
    expect(renderHook(() => useApplyHref()).result.current).toBeNull()
  })

  // Fail closed: an API that predates the field, or a features fetch that has
  // not landed, must not surface an apply link.
  it('returns null when the features payload omits the apply field', () => {
    mockFeatures = { maintenanceBanner: { enabled: false, message: {} } }
    expect(renderHook(() => useApplyHref()).result.current).toBeNull()
  })

  it('returns null when the features fetch has not resolved', () => {
    mockFeatures = undefined
    expect(renderHook(() => useApplyHref()).result.current).toBeNull()
  })
})
