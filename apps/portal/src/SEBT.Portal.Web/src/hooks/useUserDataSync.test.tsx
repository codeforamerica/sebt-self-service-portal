import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { SessionInfo } from '@/features/auth/context'

import { useUserDataSync } from './useUserDataSync'

const mockSetUserData = vi.fn()
vi.mock('@sebt/analytics', () => ({
  useDataLayer: () => ({
    setUserData: mockSetUserData,
    setPageData: vi.fn(),
    trackEvent: vi.fn(),
    pageLoad: vi.fn(),
    setPageCategory: vi.fn(),
    setPageAttribute: vi.fn(),
    setUserProfile: vi.fn(),
    get: vi.fn()
  })
}))

const mockUseAuth = vi.fn()
vi.mock('@/features/auth/context', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/context')>()
  return {
    ...actual,
    useAuth: () => mockUseAuth()
  }
})

const baseSession: SessionInfo = {
  userId: null,
  email: null,
  ial: null,
  idProofingStatus: null,
  idProofingCompletedAt: null,
  idProofingExpiresAt: null,
  isCoLoaded: null,
  expiresAt: null,
  absoluteExpiresAt: null
}

describe('useUserDataSync', () => {
  beforeEach(() => {
    mockSetUserData.mockClear()
    mockUseAuth.mockReset()
  })

  it('emits portal_id when session.userId is present', () => {
    mockUseAuth.mockReturnValue({
      session: { ...baseSession, userId: '11111111-1111-1111-1111-111111111111' },
      isAuthenticated: true
    })

    renderHook(() => useUserDataSync())

    expect(mockSetUserData).toHaveBeenCalledWith(
      'portal_id',
      '11111111-1111-1111-1111-111111111111',
      ['default', 'analytics']
    )
  })

  it('does not emit portal_id when session.userId is missing', () => {
    mockUseAuth.mockReturnValue({
      session: { ...baseSession, userId: null },
      isAuthenticated: true
    })

    renderHook(() => useUserDataSync())

    expect(mockSetUserData).not.toHaveBeenCalledWith(
      'portal_id',
      expect.anything(),
      expect.anything()
    )
  })

  it('does not emit any user data when there is no session', () => {
    mockUseAuth.mockReturnValue({ session: null, isAuthenticated: false })

    renderHook(() => useUserDataSync())

    // Only the leading `authenticated` flag fires; no portal_id, no IAL, etc.
    expect(mockSetUserData).toHaveBeenCalledTimes(1)
    expect(mockSetUserData).toHaveBeenCalledWith('authenticated', false, ['default', 'analytics'])
  })
})
