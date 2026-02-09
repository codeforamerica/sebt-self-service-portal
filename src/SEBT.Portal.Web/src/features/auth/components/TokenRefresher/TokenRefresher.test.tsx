'use client'

import { act, render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'

import { TokenRefresher } from './TokenRefresher'

// Mock the hooks
const mockLogin = vi.fn()
const mockMutate = vi.fn()

vi.mock('../../context', () => ({
  useAuth: vi.fn()
}))

vi.mock('../../api', () => ({
  useRefreshToken: () => ({
    mutate: mockMutate
  })
}))

import { useAuth } from '../../context'

describe('TokenRefresher', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.useFakeTimers()
    ;(useAuth as Mock).mockReturnValue({
      isAuthenticated: true,
      login: mockLogin
    })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('should call refresh immediately on mount when authenticated', () => {
    render(<TokenRefresher />)

    expect(mockMutate).toHaveBeenCalledTimes(1)
    expect(mockMutate).toHaveBeenCalledWith(undefined, expect.any(Object))
  })

  it('should call login with new token on successful refresh', () => {
    render(<TokenRefresher />)

    // Get the onSuccess callback from the mutate call
    const mutateCall = mockMutate.mock.calls[0] as [
      undefined,
      { onSuccess: (result: { token: string }) => void }
    ]
    const options = mutateCall[1]

    // Simulate successful refresh
    options.onSuccess({ token: 'new-jwt-token' })

    expect(mockLogin).toHaveBeenCalledWith('new-jwt-token')
  })

  it('should set up periodic refresh interval', () => {
    render(<TokenRefresher />)

    // Initial call on mount
    expect(mockMutate).toHaveBeenCalledTimes(1)

    // Advance time by 10 minutes (600000ms)
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    expect(mockMutate).toHaveBeenCalledTimes(2)

    // Advance another 10 minutes
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    expect(mockMutate).toHaveBeenCalledTimes(3)
  })

  it('should clear interval on unmount', () => {
    const { unmount } = render(<TokenRefresher />)

    // Initial call
    expect(mockMutate).toHaveBeenCalledTimes(1)

    unmount()

    // Advance time - should NOT trigger another refresh
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    // Still only the initial call
    expect(mockMutate).toHaveBeenCalledTimes(1)
  })

  it('should not refresh when not authenticated', () => {
    ;(useAuth as Mock).mockReturnValue({
      isAuthenticated: false,
      login: mockLogin
    })

    render(<TokenRefresher />)

    expect(mockMutate).not.toHaveBeenCalled()

    // Advance time - should still not call refresh
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    expect(mockMutate).not.toHaveBeenCalled()
  })

  it('should clear interval when authentication state changes to false', () => {
    const { rerender } = render(<TokenRefresher />)

    // Initial call when authenticated
    expect(mockMutate).toHaveBeenCalledTimes(1)

    // Change to unauthenticated
    ;(useAuth as Mock).mockReturnValue({
      isAuthenticated: false,
      login: mockLogin
    })

    rerender(<TokenRefresher />)

    // Advance time - should NOT trigger refresh since unauthenticated
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    // Still only the initial call
    expect(mockMutate).toHaveBeenCalledTimes(1)
  })

  it('should render nothing', () => {
    const { container } = render(<TokenRefresher />)

    expect(container).toBeEmptyDOMElement()
  })
})
