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

// Category is derived from the data layer's page.flow + page.step (the per-route
// values registered in analytics-routes.ts → DataLayerProvider). Combined as
// `flow:step` so analysts can segment by both. Falls back to whichever field is
// populated, then to `unknown` when an unmapped route renders.
function deriveCategory(dl: DataLayerRoot): string {
  const flow = dl.get('page.flow', 'analytics', '') as string
  const step = dl.get('page.step', 'analytics', '') as string
  if (flow && step) return `${flow}:${step}`
  return flow || step || 'unknown'
}

// Keys that may carry PII are dropped before the label is serialized. The
// match is case-insensitive and underscore-insensitive — `Email_Address`,
// `email_address`, and `emailAddress` all hit `emailaddress` in the set.
// This is a defensive floor — callers should still scope payloads to
// non-PII fields up front.
const PII_KEYS = new Set([
  'email',
  'emailaddress',
  'phone',
  'phonenumber',
  'phonenum',
  'name',
  'firstname',
  'lastname',
  'middlename',
  'fullname',
  'address',
  'street',
  'streetaddress',
  'streetaddress1',
  'streetaddress2',
  'city',
  'state',
  'zip',
  'zipcode',
  'postalcode',
  'dob',
  'dateofbirth',
  'birthdate',
  'ssn',
  'itin',
  'socialsecuritynumber',
  'snapid',
  'tanfid',
  'medicaidid',
  'caseid',
  'householdid'
])

function normalizeKey(key: string): string {
  return key.toLowerCase().replace(/_/g, '')
}

function scrubPii(value: unknown): unknown {
  if (value === null || typeof value !== 'object') return value
  if (Array.isArray(value)) return value.map(scrubPii)
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
    if (PII_KEYS.has(normalizeKey(k))) continue
    out[k] = scrubPii(v)
  }
  return out
}

// Safely scrub PII keys and serialize to a JSON label. SiteImprove labels must
// be a string, and we never want an analytics bridge to throw on weird payloads
// (circular refs from caller bugs, BigInts, JSON.stringify failures). Returns
// null when either scrubbing or serialization fails — the caller then omits
// the label entirely.
function safeBuildLabel(value: Record<string, unknown>): string | null {
  try {
    const scrubbed = scrubPii(value) as Record<string, unknown>
    if (Object.keys(scrubbed).length === 0) return null
    return JSON.stringify(scrubbed)
  } catch {
    return null
  }
}

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

    // Label carries the JSON-serialized event payload with known PII keys
    // dropped (see PII_KEYS). When DC confirms a per-event label scheme this
    // can be tightened to a per-event allow-list.
    const category = deriveCategory(dl)
    const label = detail.eventData ? safeBuildLabel(detail.eventData) : null
    pushToSz(
      label !== null
        ? ['event', category, detail.eventName, label]
        : ['event', category, detail.eventName]
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
