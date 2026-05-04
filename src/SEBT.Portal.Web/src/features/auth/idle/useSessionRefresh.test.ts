import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useSessionRefresh } from './useSessionRefresh'

const NOW_MS = new Date('2026-01-01T00:00:00Z').getTime()
const NOW_SEC = Math.floor(NOW_MS / 1000)
const IDLE_THRESHOLD_MS = 15 * 60 * 1000

describe('useSessionRefresh', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(NOW_MS))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  function setup(overrides: Partial<Parameters<typeof useSessionRefresh>[0]> = {}) {
    const refresh = vi.fn()
    // Default: user is always "currently active". Tests that need stale activity
    // override `getLastActivityAt`.
    const getLastActivityAt = vi.fn(() => Date.now())
    const props = {
      expiresAt: NOW_SEC + 15 * 60, // 15 min from now (sliding)
      absoluteExpiresAt: NOW_SEC + 60 * 60, // 60 min from now (absolute)
      getLastActivityAt,
      idleThresholdMs: IDLE_THRESHOLD_MS,
      refresh,
      ...overrides
    }
    const { rerender, unmount } = renderHook((p: typeof props) => useSessionRefresh(p), {
      initialProps: props
    })
    return { refresh, getLastActivityAt, props, rerender, unmount }
  }

  it('schedules refresh 60 seconds before expiresAt and fires when active', () => {
    const { refresh } = setup()

    // 15 min - 60 s = 14 min. Not yet at fire time.
    act(() => {
      vi.advanceTimersByTime(14 * 60 * 1000 - 1)
    })
    expect(refresh).not.toHaveBeenCalled()

    act(() => {
      vi.advanceTimersByTime(1)
    })
    expect(refresh).toHaveBeenCalledTimes(1)
  })

  it('does not refresh when user has been idle past the threshold', () => {
    // Activity timestamp is older than IDLE_THRESHOLD_MS at fire time.
    const staleActivity = NOW_MS - IDLE_THRESHOLD_MS - 1000
    const { refresh } = setup({ getLastActivityAt: () => staleActivity })

    act(() => {
      vi.advanceTimersByTime(15 * 60 * 1000)
    })

    expect(refresh).not.toHaveBeenCalled()
  })

  it('does not refresh once the absolute cap has been reached', () => {
    // Schedule fires at expiresAt - 60s. If absoluteExpiresAt is in the past
    // by then, refresh is suppressed even with recent activity.
    const { refresh } = setup({
      expiresAt: NOW_SEC + 15 * 60,
      absoluteExpiresAt: NOW_SEC + 5 * 60 // cap reached well before fire time
    })

    act(() => {
      vi.advanceTimersByTime(15 * 60 * 1000)
    })

    expect(refresh).not.toHaveBeenCalled()
  })

  it('does not schedule when expiresAt is null', () => {
    const { refresh } = setup({ expiresAt: null })

    act(() => {
      vi.advanceTimersByTime(60 * 60 * 1000)
    })

    expect(refresh).not.toHaveBeenCalled()
  })

  it('reschedules on a new expiresAt (post-refresh)', () => {
    const { refresh, props, rerender } = setup()

    // Don't reach the original fire time (14 min). Update expiresAt to a new value
    // (simulating /auth/status returning a fresh expiry after refresh).
    act(() => {
      vi.advanceTimersByTime(13 * 60 * 1000)
    })
    expect(refresh).not.toHaveBeenCalled()

    rerender({ ...props, expiresAt: NOW_SEC + 13 * 60 + 15 * 60 })

    // New fire time = (13min + 15min) - 60s = 27min from t=0. We're at 13min now,
    // so 14min more. Just under that should not have fired.
    act(() => {
      vi.advanceTimersByTime(14 * 60 * 1000 - 1)
    })
    expect(refresh).not.toHaveBeenCalled()

    act(() => {
      vi.advanceTimersByTime(1)
    })
    expect(refresh).toHaveBeenCalledTimes(1)
  })

  it('cancels the pending timer on unmount', () => {
    const { refresh, unmount } = setup()
    unmount()

    act(() => {
      vi.advanceTimersByTime(60 * 60 * 1000)
    })

    expect(refresh).not.toHaveBeenCalled()
  })
})
