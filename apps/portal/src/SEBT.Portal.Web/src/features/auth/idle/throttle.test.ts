import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { throttle } from './throttle'

describe('throttle', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('invokes immediately on the first call (leading edge)', () => {
    const spy = vi.fn()
    const throttled = throttle(spy, 1000)

    throttled()

    expect(spy).toHaveBeenCalledTimes(1)
  })

  it('drops calls within the interval', () => {
    const spy = vi.fn()
    const throttled = throttle(spy, 1000)

    throttled()
    vi.advanceTimersByTime(500)
    throttled()
    throttled()

    expect(spy).toHaveBeenCalledTimes(1)
  })

  it('invokes again after the interval has elapsed', () => {
    const spy = vi.fn()
    const throttled = throttle(spy, 1000)

    throttled()
    vi.advanceTimersByTime(1000)
    throttled()

    expect(spy).toHaveBeenCalledTimes(2)
  })

  it('forwards arguments to the wrapped function', () => {
    const spy = vi.fn()
    const throttled = throttle(spy, 1000)

    throttled('a', 42)

    expect(spy).toHaveBeenCalledWith('a', 42)
  })
})
