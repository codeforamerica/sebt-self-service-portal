/**
 * DOM event bridge that listens to DataLayer CustomEvents and forwards
 * them to Mixpanel. The data layer pre-merges analytics-scoped page + user
 * context into eventData, so the bridge passes payloads through as-is.
 *
 * @see docs/tdd/analytics-data-layer.md — "DOM Bridge & Sample Integration"
 */

import { type DataLayerRoot, subscribeToPageViews } from './data-layer'

/** Subset of the Mixpanel SDK API used by the bridge. */
interface MixpanelLike {
  init: (token: string, config: Record<string, unknown>) => void
  track: (eventName: string, properties?: Record<string, unknown>) => void
  track_pageview?: (properties?: Record<string, unknown>) => void
}

declare global {
  interface Window {
    mixpanel?: MixpanelLike
  }
}

function attachBridge(dl: DataLayerRoot): () => void {
  const mp = window.mixpanel!

  function handleEventTracked(event: Event) {
    const detail = (event as CustomEvent).detail as {
      eventName?: string
      eventData?: Record<string, unknown>
    } | undefined

    if (!detail?.eventName) return

    // The data layer merges analytics-scoped page + user context into every
    // event's eventData (see _trackEvent in data-layer.ts), so the bridge
    // forwards the payload as-is.
    mp.track(detail.eventName, detail.eventData)
  }

  const eventTrackedEvent = dl.eventTypes.EVENT_TRACKED!
  document.addEventListener(eventTrackedEvent, handleEventTracked)

  // Page views go through the shared subscription, which also replays the
  // page_load that fired before this bridge attached — Mixpanel loads from a CDN,
  // so it always attaches after the first page_load. Fall back to mp.track for
  // older SDK builds without track_pageview.
  const detachPageViews = subscribeToPageViews(dl, (_eventName, eventData) => {
    if (mp.track_pageview) {
      mp.track_pageview(eventData)
    } else {
      mp.track('page_view', eventData)
    }
  })

  return () => {
    document.removeEventListener(eventTrackedEvent, handleEventTracked)
    detachPageViews()
  }
}

export function initMixpanelBridge(token: string): () => void {
  const mp = window.mixpanel
  // Only `init` is required up-front. The official Mixpanel boot snippet
  // defines `init` immediately on the queue stub but only attaches `track`
  // (and the rest of the API surface) the first time `init()` is called —
  // checking for `track` here would always fail and never bootstrap the SDK.
  if (!mp?.init) {
    return () => {}
  }

  // Session replay disabled by default — recording user sessions risks capturing
  // PII (SSNs, addresses, eligibility data). Re-enable explicitly only after
  // privacy review and DOM masking configuration.
  mp.init(token, {
    track_pageview: false,
    autocapture: true,
    record_sessions_percent: 0
  })

  let bridgeTeardown: (() => void) | undefined

  // If data layer is already initialized, attach immediately
  if (window.digitalData?.initialized) {
    bridgeTeardown = attachBridge(window.digitalData)
    return () => bridgeTeardown?.()
  }

  // Otherwise wait for the initialization event
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
