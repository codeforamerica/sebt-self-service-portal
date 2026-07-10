/**
 * Web Vitals → data layer adapter.
 *
 * Subscribes to the five core Web Vitals via the `web-vitals` library and forwards each
 * metric to the data layer as a `web_vitals` event when it finalizes. Metrics finalize at
 * different times (TTFB/FCP early, LCP on first interaction, CLS/INP at page-hide), so they
 * cannot ride on the page_load event; `page_instance_id` and `initial_path` tie the events
 * back to the hard page load they measured. Browsers lacking support for a metric simply
 * never fire its callback — the library feature-detects internally.
 */

import { onCLS, onFCP, onINP, onLCP, onTTFB, type Metric } from 'web-vitals'

import type { DataLayerRoot } from './data-layer'
import { WEB_VITALS } from './events'

function newPageInstanceId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `pi-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
}

/** CLS is a unitless score; every other vital is a duration in milliseconds. */
function normalizeValue(metric: Metric): number {
  if (metric.name === 'CLS') {
    return Math.round(metric.value * 10000) / 10000
  }
  return Math.round(metric.value)
}

/**
 * Subscribes all five Web Vitals and emits each as a `web_vitals` event at most once per
 * hard page load. Returns a cleanup function that stops further emissions (the library has
 * no unsubscribe, so cleanup gates the callbacks instead).
 */
export function initWebVitals(dl: Pick<DataLayerRoot, 'trackEvent'>): () => void {
  const pageInstanceId = newPageInstanceId()
  const initialPath = window.location.pathname
  const reported = new Set<Metric['name']>()
  let disposed = false

  function report(metric: Metric): void {
    if (disposed || reported.has(metric.name)) return
    reported.add(metric.name)

    dl.trackEvent(WEB_VITALS, {
      metric_name: metric.name.toLowerCase(),
      metric_value: normalizeValue(metric),
      metric_rating: metric.rating,
      page_instance_id: pageInstanceId,
      initial_path: initialPath
    })
  }

  onTTFB(report)
  onFCP(report)
  onLCP(report)
  onCLS(report)
  onINP(report)

  return () => {
    disposed = true
  }
}
