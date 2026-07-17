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

const PAGE_VIEWED = 'digitalData:PageViewed'

/** Simulates the first page_load event the data layer emits after PageTracker runs. */
function firePageViewed(): void {
  document.dispatchEvent(new CustomEvent(PAGE_VIEWED, { bubbles: true }))
}

describe('initWebVitals', () => {
  const trackEvent = vi.fn()
  const pageValues: Record<string, unknown> = {}
  const get = vi.fn((path: string) => pageValues[path])
  const dl = { trackEvent, get, eventTypes: { PAGE_VIEWED } }

  beforeEach(() => {
    trackEvent.mockClear()
    get.mockClear()
    for (const key of Object.keys(callbacks)) delete callbacks[key]
    for (const key of Object.keys(pageValues)) delete pageValues[key]
    Object.assign(pageValues, {
      'page.name': 'Dashboard',
      'page.language': 'en',
      'page.environment': 'test',
      'page.application': 'sebt-portal',
      'page.flow': 'dashboard',
      'page.step': '1'
    })
  })

  it('defers subscribing until the first page load so page context is populated', () => {
    const cleanup = initWebVitals(dl)

    expect(Object.keys(callbacks)).toHaveLength(0)

    firePageViewed()

    expect(Object.keys(callbacks).sort()).toEqual(['CLS', 'FCP', 'INP', 'LCP', 'TTFB'])
    cleanup()
  })

  it('emits a web_vitals event with lowercase name, integer ms, and rating for a timing metric', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

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
    cleanup()
  })

  it('rounds CLS to four decimals instead of milliseconds', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

    callbacks.CLS!(fakeMetric('CLS', 0.10236789, 'needs-improvement'))

    expect(trackEvent).toHaveBeenCalledWith(
      WEB_VITALS,
      expect.objectContaining({
        metric_name: 'cls',
        metric_value: 0.1024,
        metric_rating: 'needs-improvement'
      })
    )
    cleanup()
  })

  it('emits each metric at most once per page load', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

    callbacks.INP!(fakeMetric('INP', 180, 'good'))
    callbacks.INP!(fakeMetric('INP', 320, 'needs-improvement'))

    expect(trackEvent).toHaveBeenCalledTimes(1)
    cleanup()
  })

  it('stamps every event with the page context captured at the measured page load', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

    // A soft navigation changes the live data layer after the snapshot…
    pageValues['page.name'] = 'Address Update'
    pageValues['page.flow'] = 'address_update'

    callbacks.TTFB!(fakeMetric('TTFB', 120.4, 'good'))
    callbacks.CLS!(fakeMetric('CLS', 0.01, 'good'))

    // …but both events still carry the values from the load they measured.
    for (const [, eventData] of trackEvent.mock.calls) {
      expect(eventData).toMatchObject({
        name: 'Dashboard',
        language: 'en',
        environment: 'test',
        application: 'sebt-portal',
        flow: 'dashboard',
        step: '1'
      })
    }
    cleanup()
  })

  it('omits page fields that are unset at the measured page load', () => {
    delete pageValues['page.flow']
    delete pageValues['page.step']
    const cleanup = initWebVitals(dl)
    firePageViewed()

    callbacks.FCP!(fakeMetric('FCP', 900.2, 'good'))

    const [, eventData] = trackEvent.mock.calls[0]!
    expect(eventData).not.toHaveProperty('flow')
    expect(eventData).not.toHaveProperty('step')
    expect(eventData.name).toBe('Dashboard')
    cleanup()
  })

  it('only snapshots page context from the first page load', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

    pageValues['page.name'] = 'Second Page'
    firePageViewed()

    callbacks.LCP!(fakeMetric('LCP', 2000, 'good'))

    const [, eventData] = trackEvent.mock.calls[0]!
    expect(eventData.name).toBe('Dashboard')
    cleanup()
  })

  it('stops emitting after cleanup', () => {
    const cleanup = initWebVitals(dl)
    firePageViewed()

    cleanup()
    callbacks.LCP!(fakeMetric('LCP', 2000, 'good'))

    expect(trackEvent).not.toHaveBeenCalled()
  })

  it('never subscribes when cleaned up before the first page load', () => {
    const cleanup = initWebVitals(dl)

    cleanup()
    firePageViewed()

    expect(Object.keys(callbacks)).toHaveLength(0)
    expect(trackEvent).not.toHaveBeenCalled()
  })
})
