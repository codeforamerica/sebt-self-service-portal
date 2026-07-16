/**
 * Web Vitals → data layer adapter.
 *
 * Subscribes to the five core Web Vitals via the `web-vitals` library and forwards each
 * metric to the data layer as a `web_vitals` event when it finalizes. Metrics finalize at
 * different times (TTFB/FCP early, LCP on first interaction, CLS/INP at page-hide), so they
 * cannot ride on the page_load event. Instead, each event carries the same page dimensions
 * page_load sends (name, language, environment, application, flow, step), snapshotted when
 * the first page_load fires so every metric is attributed to the hard page load it measured
 * — even CLS/INP, which finalize after any soft navigations have changed the live data
 * layer. Browsers lacking support for a metric simply never fire its callback — the library
 * feature-detects internally.
 */

import { onCLS, onFCP, onINP, onLCP, onTTFB, type Metric } from 'web-vitals'

import type { DataLayerRoot } from './data-layer'
import { WEB_VITALS } from './events'

/**
 * Page dimensions mirrored from page_load events, keyed as page_load sends them
 * (top-level page scalars, unprefixed) so dashboards can join on the same fields.
 */
const PAGE_CONTEXT_KEYS = ['name', 'language', 'environment', 'application', 'flow', 'step'] as const

/** CLS is a unitless score; every other vital is a duration in milliseconds. */
function normalizeValue(metric: Metric): number {
  if (metric.name === 'CLS') {
    return Math.round(metric.value * 10000) / 10000
  }
  return Math.round(metric.value)
}

/**
 * Emits each of the five Web Vitals as a `web_vitals` event at most once per hard page
 * load. Subscription waits for the data layer's first page_load (PageViewed) so the page
 * context it snapshots is fully populated — PageTracker sets name/language/flow/step in a
 * deferred frame, after this adapter is initialized. The one-frame delay loses no data:
 * the web-vitals library replays earlier entries via buffered PerformanceObservers.
 * Returns a cleanup function that stops further emissions (the library has no unsubscribe,
 * so cleanup gates the callbacks instead).
 */
export function initWebVitals(
  dl: Pick<DataLayerRoot, 'trackEvent' | 'get' | 'eventTypes'>
): () => void {
  const reported = new Set<Metric['name']>()
  let disposed = false
  const pageContext: Record<string, unknown> = {}

  function report(metric: Metric): void {
    if (disposed || reported.has(metric.name)) return
    reported.add(metric.name)

    dl.trackEvent(WEB_VITALS, {
      metric_name: metric.name.toLowerCase(),
      metric_value: normalizeValue(metric),
      metric_rating: metric.rating,
      ...pageContext
    })
  }

  function subscribe(): void {
    if (disposed) return

    for (const key of PAGE_CONTEXT_KEYS) {
      const value = dl.get(`page.${key}`)
      if (value !== undefined) {
        pageContext[key] = value
      }
    }

    onTTFB(report)
    onFCP(report)
    onLCP(report)
    onCLS(report)
    onINP(report)
  }

  const pageViewedEvent = dl.eventTypes.PAGE_VIEWED!
  document.addEventListener(pageViewedEvent, subscribe, { once: true })

  return () => {
    disposed = true
    document.removeEventListener(pageViewedEvent, subscribe)
  }
}
