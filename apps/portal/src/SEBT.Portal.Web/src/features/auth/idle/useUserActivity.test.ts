import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useUserActivity } from './useUserActivity'

describe('useUserActivity', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('initializes lastActivityAt to the current time', () => {
    const { result } = renderHook(() => useUserActivity())

    expect(result.current.getLastActivityAt()).toBe(Date.now())
  })

  it('updates lastActivityAt on mousedown', () => {
    const { result } = renderHook(() => useUserActivity())
    const initial = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(5000)
      window.dispatchEvent(new Event('mousedown'))
    })

    expect(result.current.getLastActivityAt()).toBeGreaterThan(initial)
    expect(result.current.getLastActivityAt()).toBe(Date.now())
  })

  it('updates lastActivityAt on keydown', () => {
    const { result } = renderHook(() => useUserActivity())
    const initial = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(2000)
      window.dispatchEvent(new Event('keydown'))
    })

    expect(result.current.getLastActivityAt()).toBeGreaterThan(initial)
  })

  it('updates lastActivityAt on visibilitychange', () => {
    // A user returning to the tab is meaningful activity — otherwise a refresh
    // could fire just after they switch back, before they've moved the mouse.
    const { result } = renderHook(() => useUserActivity())
    const initial = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(3000)
      window.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.getLastActivityAt()).toBeGreaterThan(initial)
  })

  it('throttles rapid events to at most once per second', () => {
    const { result } = renderHook(() => useUserActivity())
    const initial = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(100)
      window.dispatchEvent(new Event('mousedown'))
    })
    const firstUpdate = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(500)
      window.dispatchEvent(new Event('mousedown'))
    })

    // Second event was within the 1s throttle window — timestamp shouldn't change.
    expect(result.current.getLastActivityAt()).toBe(firstUpdate)
    expect(firstUpdate).toBeGreaterThan(initial)
  })

  it('removes listeners on unmount', () => {
    const { result, unmount } = renderHook(() => useUserActivity())
    unmount()
    const beforeEvent = result.current.getLastActivityAt()

    act(() => {
      vi.advanceTimersByTime(5000)
      window.dispatchEvent(new Event('mousedown'))
    })

    expect(result.current.getLastActivityAt()).toBe(beforeEvent)
  })
})
