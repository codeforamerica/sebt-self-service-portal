import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { DataLayer } from './data-layer'
import { initSiteImproveBridge } from './siteimprove-bridge'

describe('initSiteImproveBridge', () => {
  let pushSpy: ReturnType<typeof vi.fn>
  let teardown: (() => void) | undefined

  beforeEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
    pushSpy = vi.fn()
    ;(window as unknown as Record<string, unknown>)._sz = { push: pushSpy }
    teardown = undefined
    // Reset path so deriveCategory() returns a predictable bucket per test.
    window.history.replaceState({}, '', '/')
  })

  afterEach(() => {
    teardown?.()
    delete (window as unknown as Record<string, unknown>)._sz
    delete (window as unknown as Record<string, unknown>).digitalData
    window.history.replaceState({}, '', '/')
  })

  it('forwards page_load to _sz with a trackdynamic command', () => {
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.pageLoad({ name: 'Dashboard' })

    expect(pushSpy).toHaveBeenCalledTimes(1)
    const [cmd] = pushSpy.mock.calls[0] as [unknown[]]
    expect(cmd[0]).toBe('trackdynamic')
    expect(cmd[1]).toMatchObject({
      url: expect.any(String),
      ref: expect.any(String),
      title: expect.any(String)
    })
  })

  it('forwards trackEvent with the path-derived category and event name as action', () => {
    window.history.replaceState({}, '', '/cards/info')
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('cta_click', { target: 'replace_card' })

    expect(pushSpy).toHaveBeenCalledTimes(1)
    expect(pushSpy.mock.calls[0][0]).toEqual([
      'event',
      'cards',
      'cta_click',
      JSON.stringify({ target: 'replace_card' })
    ])
  })

  it('uses "root" as the category when on /', () => {
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('page_load')

    expect(pushSpy.mock.calls[0][0]).toEqual(['event', 'root', 'page_load'])
  })

  it('omits the label when the event has no data', () => {
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('page_load')

    expect(pushSpy.mock.calls[0][0]).toEqual(['event', 'root', 'page_load'])
  })

  it('strips PII keys from the label payload before serializing', () => {
    window.history.replaceState({}, '', '/profile/address')
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('cta_click', {
      target: 'save_address',
      email: 'user@example.com',
      firstName: 'Alex',
      lastName: 'Smith',
      address: '123 Main St',
      zip: '20001',
      household_type: 'co_loaded_only'
    })

    expect(pushSpy.mock.calls[0][0]).toEqual([
      'event',
      'profile',
      'cta_click',
      JSON.stringify({ target: 'save_address', household_type: 'co_loaded_only' })
    ])
  })

  it('strips PII recursively from nested objects', () => {
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('cta_click', {
      target: 'submit',
      user: { id: 'abc-123', email: 'user@example.com', name: 'Alex' }
    })

    expect(pushSpy.mock.calls[0][0]).toEqual([
      'event',
      'root',
      'cta_click',
      JSON.stringify({ target: 'submit', user: { id: 'abc-123' } })
    ])
  })

  it('omits the label when every key in the payload is PII', () => {
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.trackEvent('cta_click', {
      email: 'user@example.com',
      phone: '202-555-0100'
    })

    expect(pushSpy.mock.calls[0][0]).toEqual(['event', 'root', 'cta_click'])
  })

  it('seeds _sz as an array if SiteImprove script has not loaded yet', () => {
    delete (window as unknown as Record<string, unknown>)._sz
    new DataLayer('digitalData')
    teardown = initSiteImproveBridge()

    window.digitalData!.pageLoad({ name: 'Login' })

    expect(Array.isArray((window as unknown as { _sz: unknown })._sz)).toBe(true)
    expect((window as unknown as { _sz: unknown[] })._sz).toHaveLength(1)
  })

  it('attaches when DataLayer initializes after the bridge', () => {
    teardown = initSiteImproveBridge()
    expect(pushSpy).not.toHaveBeenCalled()

    new DataLayer('digitalData')
    window.digitalData!.pageLoad({ name: 'Deferred' })

    expect(pushSpy).toHaveBeenCalledTimes(1)
  })

  it('returns a teardown that detaches listeners', () => {
    new DataLayer('digitalData')
    const localTeardown = initSiteImproveBridge()

    localTeardown()

    window.digitalData!.pageLoad({ name: 'After teardown' })
    window.digitalData!.trackEvent('cta_click')

    expect(pushSpy).not.toHaveBeenCalled()
  })
})
