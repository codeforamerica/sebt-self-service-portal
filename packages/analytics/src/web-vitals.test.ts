import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Metric } from 'web-vitals'

import { WEB_VITALS } from './events'
import { initWebVitals } from './web-vitals'

// Capture the callback each subscription registers so tests can fire fake metrics.
const callbacks: Record<string, (metric: Metric) => void> = {}

vi.mock('web-vitals', () => ({
  onCLS: vi.fn((cb: (metric: Metric) => void) => {
    callbacks.CLS = cb
  }),
  onFCP: vi.fn((cb: (metric: Metric) => void) => {
    callbacks.FCP = cb
  }),
  onINP: vi.fn((cb: (metric: Metric) => void) => {
    callbacks.INP = cb
  }),
  onLCP: vi.fn((cb: (metric: Metric) => void) => {
    callbacks.LCP = cb
  }),
  onTTFB: vi.fn((cb: (metric: Metric) => void) => {
    callbacks.TTFB = cb
  })
}))

function fakeMetric(name: Metric['name'], value: number, rating: Metric['rating']): Metric {
  return {
    name,
    value,
    rating,
    delta: value,
    id: `v5-${name}-test`,
    entries: [],
    navigationType: 'navigate'
  } as Metric
}

describe('initWebVitals', () => {
  const trackEvent = vi.fn()

  beforeEach(() => {
    trackEvent.mockClear()
    for (const key of Object.keys(callbacks)) delete callbacks[key]
  })

  it('subscribes to all five metrics', () => {
    initWebVitals({ trackEvent })

    expect(Object.keys(callbacks).sort()).toEqual(['CLS', 'FCP', 'INP', 'LCP', 'TTFB'])
  })

  it('emits a web_vitals event with lowercase name, integer ms, and rating for a timing metric', () => {
    initWebVitals({ trackEvent })

    callbacks.LCP!(fakeMetric('LCP', 2483.7, 'good'))

    expect(trackEvent).toHaveBeenCalledTimes(1)
    expect(trackEvent).toHaveBeenCalledWith(
      WEB_VITALS,
      expect.objectContaining({
        metric_name: 'lcp',
        metric_value: 2484,
        metric_rating: 'good'
      })
    )
  })

  it('rounds CLS to four decimals instead of milliseconds', () => {
    initWebVitals({ trackEvent })

    callbacks.CLS!(fakeMetric('CLS', 0.10236789, 'needs-improvement'))

    expect(trackEvent).toHaveBeenCalledWith(
      WEB_VITALS,
      expect.objectContaining({
        metric_name: 'cls',
        metric_value: 0.1024,
        metric_rating: 'needs-improvement'
      })
    )
  })

  it('emits each metric at most once per page load', () => {
    initWebVitals({ trackEvent })

    callbacks.INP!(fakeMetric('INP', 180, 'good'))
    callbacks.INP!(fakeMetric('INP', 320, 'needs-improvement'))

    expect(trackEvent).toHaveBeenCalledTimes(1)
  })

  it('stamps every event with the same page_instance_id and initial_path', () => {
    initWebVitals({ trackEvent })

    callbacks.TTFB!(fakeMetric('TTFB', 120.4, 'good'))
    callbacks.FCP!(fakeMetric('FCP', 900.2, 'good'))

    const [, first] = trackEvent.mock.calls[0]!
    const [, second] = trackEvent.mock.calls[1]!
    expect(first.page_instance_id).toEqual(expect.any(String))
    expect(first.page_instance_id).toBe(second.page_instance_id)
    expect(first.initial_path).toBe(window.location.pathname)
    expect(second.initial_path).toBe(window.location.pathname)
  })

  it('stops emitting after cleanup', () => {
    const cleanup = initWebVitals({ trackEvent })

    cleanup()
    callbacks.LCP!(fakeMetric('LCP', 2000, 'good'))

    expect(trackEvent).not.toHaveBeenCalled()
  })
})
