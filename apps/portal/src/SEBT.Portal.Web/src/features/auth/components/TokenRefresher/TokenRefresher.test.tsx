'use client'

import { act, render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'

import { TokenRefresher } from './TokenRefresher'

// Substantive scheduling/activity logic lives in `useUserActivity` and
// `useSessionRefresh` — those hooks have their own tests. This file only
// verifies that the component wires session/expiry from `useAuth` to those
// hooks and re-reads `/auth/status` after a successful refresh.

const mockLogin = vi.fn()
const mockMutate = vi.fn()

vi.mock('../../context', () => ({ useAuth: vi.fn() }))
vi.mock('../../api', () => ({
  useRefreshToken: () => ({ mutate: mockMutate })
}))

import { useAuth } from '../../context'

const NOW_MS = new Date('2026-01-01T00:00:00Z').getTime()
const NOW_SEC = Math.floor(NOW_MS / 1000)

describe('TokenRefresher', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.useFakeTimers()
    vi.setSystemTime(new Date(NOW_MS))
    ;(useAuth as Mock).mockReturnValue({
      session: {
        email: 'user@example.com',
        ial: '1plus',
        idProofingStatus: 2,
        idProofingCompletedAt: null,
        idProofingExpiresAt: null,
        isCoLoaded: false,
        expiresAt: NOW_SEC + 15 * 60,
        absoluteExpiresAt: NOW_SEC + 60 * 60
      },
      login: mockLogin
    })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders nothing', () => {
    const { container } = render(<TokenRefresher />)
    expect(container).toBeEmptyDOMElement()
  })

  it('schedules a refresh keyed off session.expiresAt and re-reads on success', () => {
    render(<TokenRefresher />)

    // No upfront mutate — schedule waits until just before expiresAt.
    expect(mockMutate).not.toHaveBeenCalled()

    // Simulate an active user (an event in the throttle window).
    act(() => {
      window.dispatchEvent(new Event('mousedown'))
    })

    // Fire just past 14 min (15 min - 60 s safety margin).
    act(() => {
      vi.advanceTimersByTime(14 * 60 * 1000)
    })

    expect(mockMutate).toHaveBeenCalledTimes(1)

    // Simulate refresh success → component should re-read /auth/status.
    const [, options] = mockMutate.mock.calls[0] as [undefined, { onSuccess: () => void }]
    options.onSuccess()
    expect(mockLogin).toHaveBeenCalledWith()
  })

  it('does not schedule a refresh when there is no session', () => {
    ;(useAuth as Mock).mockReturnValue({ session: null, login: mockLogin })

    render(<TokenRefresher />)

    act(() => {
      vi.advanceTimersByTime(60 * 60 * 1000)
    })

    expect(mockMutate).not.toHaveBeenCalled()
  })
})
