/**
 * DOM event bridge that listens to DataLayer CustomEvents and forwards
 * them to Amplitude. Forwards both PageViewed (page_load) and EventTracked
 * events. 
 *
 * @see docs/tdd/analytics-data-layer.md — "DOM Bridge & Sample Integration"
 */

import { type DataLayerRoot, subscribeToPageViews } from './data-layer'

/** Subset of the Amplitude Browser SDK API used by the bridge. */
export interface AmplitudeLike {
  init: (apiKey: string, config?: Record<string, unknown>) => unknown
  track: (eventName: string, properties?: Record<string, unknown>) => unknown
}

function attachBridge(dl: DataLayerRoot, amplitude: AmplitudeLike): () => void {
  function forwardEvent(event: Event) {
    const detail = (event as CustomEvent).detail as
      | {
          eventName?: string
          eventData?: Record<string, unknown>
        }
      | undefined

    if (!detail?.eventName) return

    amplitude.track(detail.eventName, detail.eventData)
  }

  const eventTrackedEvent = dl.eventTypes.EVENT_TRACKED!
  document.addEventListener(eventTrackedEvent, forwardEvent)

  // Page views go through the shared subscription, which also replays the
  // page_load that may have fired before this bridge attached (e.g. the OIDC
  // callback or dashboard landing on a fresh page load) — so Amplitude can't
  // silently miss the current page view.
  const detachPageViews = subscribeToPageViews(dl, (eventName, eventData) =>
    amplitude.track(eventName, eventData)
  )

  return () => {
    document.removeEventListener(eventTrackedEvent, forwardEvent)
    detachPageViews()
  }
}

export function initAmplitudeBridge(apiKey: string, amplitude: AmplitudeLike): () => void {
  // Privacy posture:
  // - identityStorage: 'none'       → no cross-session user identity persistence
  // - trackingOptions.ipAddress: false → do not capture client IP
  //
  // Session Replay, Guides, and Surveys are separate Amplitude products that
  // require opting in via additional plugins — not enabled here by construction.
  try {
    amplitude.init(apiKey, {
      defaultTracking: true,
      autocapture: true,
      identityStorage: 'none',
      trackingOptions: {
        ipAddress: false
      }
    })
  } catch (err) {
    console.warn('[amplitude-bridge] amplitude.init failed; bridge disabled', err)
    return () => {}
  }

  let bridgeTeardown: (() => void) | undefined

  // If data layer is already initialized, attach immediately
  if (window.digitalData?.initialized) {
    bridgeTeardown = attachBridge(window.digitalData, amplitude)
    return () => bridgeTeardown?.()
  }

  // Otherwise wait for the initialization event
  function handleInitialized(event: Event) {
    const rootElement = (event as CustomEvent).detail?.rootElement as string | undefined
    if (!rootElement) {
      console.warn('[amplitude-bridge] DataLayer:Initialized fired without rootElement; bridge not attached')
      return
    }

    const dl = (window as unknown as Record<string, unknown>)[rootElement] as
      | DataLayerRoot
      | undefined
    if (dl) {
      bridgeTeardown = attachBridge(dl, amplitude)
    } else {
      console.warn(
        `[amplitude-bridge] DataLayer:Initialized referenced rootElement "${rootElement}" but window["${rootElement}"] is not a data layer; bridge not attached`
      )
    }

    document.removeEventListener('DataLayer:Initialized', handleInitialized)
  }

  document.addEventListener('DataLayer:Initialized', handleInitialized)

  return () => {
    document.removeEventListener('DataLayer:Initialized', handleInitialized)
    bridgeTeardown?.()
  }
}
