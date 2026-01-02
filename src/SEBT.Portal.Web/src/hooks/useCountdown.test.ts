import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useCountdown } from './useCountdown'

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('should initialize with zero seconds and inactive state', () => {
    const { result } = renderHook(() => useCountdown())

    expect(result.current.secondsRemaining).toBe(0)
    expect(result.current.isActive).toBe(false)
  })

  it('should start countdown when start is called', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(30)
    })

    expect(result.current.secondsRemaining).toBe(30)
    expect(result.current.isActive).toBe(true)
  })

  it('should decrement countdown every second', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(30)
    })

    expect(result.current.secondsRemaining).toBe(30)

    act(() => {
      vi.advanceTimersByTime(1000)
    })

    expect(result.current.secondsRemaining).toBe(29)

    act(() => {
      vi.advanceTimersByTime(5000)
    })

    expect(result.current.secondsRemaining).toBe(24)
  })

  it('should stop at zero and become inactive', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(3)
    })

    expect(result.current.secondsRemaining).toBe(3)
    expect(result.current.isActive).toBe(true)

    act(() => {
      vi.advanceTimersByTime(3000)
    })

    expect(result.current.secondsRemaining).toBe(0)
    expect(result.current.isActive).toBe(false)
  })

  it('should not go below zero', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(2)
    })

    act(() => {
      vi.advanceTimersByTime(5000)
    })

    expect(result.current.secondsRemaining).toBe(0)
    expect(result.current.isActive).toBe(false)
  })

  it('should reset countdown when reset is called', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(30)
    })

    act(() => {
      vi.advanceTimersByTime(5000)
    })

    expect(result.current.secondsRemaining).toBe(25)

    act(() => {
      result.current.reset()
    })

    expect(result.current.secondsRemaining).toBe(0)
    expect(result.current.isActive).toBe(false)
  })

  it('should restart countdown when start is called while active', () => {
    const { result } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(30)
    })

    act(() => {
      vi.advanceTimersByTime(10000)
    })

    expect(result.current.secondsRemaining).toBe(20)

    act(() => {
      result.current.start(60)
    })

    expect(result.current.secondsRemaining).toBe(60)
    expect(result.current.isActive).toBe(true)
  })

  it('should cleanup interval on unmount', () => {
    const { result, unmount } = renderHook(() => useCountdown())

    act(() => {
      result.current.start(30)
    })

    const clearIntervalSpy = vi.spyOn(global, 'clearInterval')

    unmount()

    expect(clearIntervalSpy).toHaveBeenCalled()
    clearIntervalSpy.mockRestore()
  })
})
