/**
 * DOM event bridge that forwards DataLayer CustomEvents to SiteImprove
 * Analytics. SiteImprove's tracking script (loaded separately) creates a
 * global `_sz` queue; we push tracking calls onto it.
 *
 * Event shape SiteImprove expects:
 *   - SPA page view: ['trackdynamic', { url, ref, title }]
 *   - Custom event:  ['event', category, action, label?]
 *
 * @see https://help.siteimprove.com/support/solutions/articles/80000863895
 */

import type { DataLayerRoot } from './data-layer'

type SzCommand = ['trackdynamic', { url: string; ref: string; title: string }] | ['event', string, string, string?]

declare global {
  interface Window {
    _sz?: { push: (cmd: SzCommand) => void } | SzCommand[]
  }
}

const EVENT_CATEGORY = 'sebt-portal'

function pushToSz(cmd: SzCommand): void {
  // The official snippet seeds `_sz = _sz || []` before the script tag loads.
  // After the script loads, _sz is replaced with an object whose .push delivers
  // events. Either form supports .push(), so we don't need to branch.
  if (!window._sz) {
    window._sz = []
  }
  window._sz.push(cmd)
}

function attachBridge(dl: DataLayerRoot): () => void {
  function handlePageViewed() {
    pushToSz([
      'trackdynamic',
      {
        url: window.location.pathname + window.location.search,
        ref: document.referrer,
        title: document.title
      }
    ])
  }

  function handleEventTracked(event: Event) {
    const detail = (event as CustomEvent).detail as
      | { eventName?: string; eventData?: Record<string, unknown> }
      | undefined
    if (!detail?.eventName) return

    // PLACEHOLDER mapping (DC-272): SiteImprove labels are typically short strings,
    // but DC has not defined per-event labels yet. Until they do, we serialize the
    // whole eventData as JSON so analysts can still see context. Replace with a
    // per-event label scheme once DC confirms what they want to track.
    const hasData = detail.eventData && Object.keys(detail.eventData).length > 0
    pushToSz(
      hasData
        ? ['event', EVENT_CATEGORY, detail.eventName, JSON.stringify(detail.eventData)]
        : ['event', EVENT_CATEGORY, detail.eventName]
    )
  }

  const pageViewedEvent = dl.eventTypes.PAGE_VIEWED!
  const eventTrackedEvent = dl.eventTypes.EVENT_TRACKED!

  document.addEventListener(pageViewedEvent, handlePageViewed)
  document.addEventListener(eventTrackedEvent, handleEventTracked)

  return () => {
    document.removeEventListener(pageViewedEvent, handlePageViewed)
    document.removeEventListener(eventTrackedEvent, handleEventTracked)
  }
}

export function initSiteImproveBridge(): () => void {
  let bridgeTeardown: (() => void) | undefined

  if (window.digitalData?.initialized) {
    bridgeTeardown = attachBridge(window.digitalData)
    return () => bridgeTeardown?.()
  }

  function handleInitialized(event: Event) {
    const rootElement = (event as CustomEvent).detail?.rootElement as string | undefined
    if (!rootElement) return

    const dl = (window as unknown as Record<string, unknown>)[rootElement] as
      | DataLayerRoot
      | undefined
    if (dl) {
      bridgeTeardown = attachBridge(dl)
    }

    document.removeEventListener('DataLayer:Initialized', handleInitialized)
  }

  document.addEventListener('DataLayer:Initialized', handleInitialized)

  return () => {
    document.removeEventListener('DataLayer:Initialized', handleInitialized)
    bridgeTeardown?.()
  }
}
