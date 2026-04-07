import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'

import { DataLayer } from './data-layer'
import { initMixpanelBridge } from './mixpanel-bridge'

function createMixpanelStub() {
  return {
    init: vi.fn(),
    track: vi.fn(),
    track_pageview: vi.fn()
  }
}

describe('initMixpanelBridge', () => {
  let mixpanelStub: ReturnType<typeof createMixpanelStub>

  beforeEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
    mixpanelStub = createMixpanelStub()
    ;(window as unknown as Record<string, unknown>).mixpanel = mixpanelStub
  })

  afterEach(() => {
    delete (window as unknown as Record<string, unknown>).mixpanel
    delete (window as unknown as Record<string, unknown>).digitalData
  })

  describe('initialization', () => {
    it('calls mixpanel.init with the provided token', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      expect(mixpanelStub.init).toHaveBeenCalledWith(
        'test-token',
        expect.objectContaining({ track_pageview: false })
      )
    })

    it('attaches listeners immediately when data layer is already initialized', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      // Trigger a page view to prove listeners are attached
      window.digitalData!.pageLoad({ name: 'Test' })

      expect(mixpanelStub.track_pageview).toHaveBeenCalledTimes(1)
    })

    it('waits for DataLayer:Initialized when data layer is not yet ready', () => {
      // Initialize bridge BEFORE data layer exists
      initMixpanelBridge('test-token')

      // No page view tracking yet
      expect(mixpanelStub.track_pageview).not.toHaveBeenCalled()

      // Now create the data layer — this fires DataLayer:Initialized
      new DataLayer('digitalData')
      window.digitalData!.pageLoad({ name: 'Deferred' })

      expect(mixpanelStub.track_pageview).toHaveBeenCalledTimes(1)
    })

    it('returns a teardown function that removes listeners', () => {
      new DataLayer('digitalData')
      const teardown = initMixpanelBridge('test-token')

      teardown()

      window.digitalData!.pageLoad({ name: 'After Teardown' })
      window.digitalData!.trackEvent('click', { target: 'button' })

      expect(mixpanelStub.track_pageview).not.toHaveBeenCalled()
      expect(mixpanelStub.track).not.toHaveBeenCalled()
    })
  })

  describe('PageViewed forwarding', () => {
    it('forwards page_load events to mixpanel.track_pageview', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Dashboard')
      window.digitalData!.page.set('flow', 'dashboard')

      initMixpanelBridge('test-token')
      window.digitalData!.pageLoad()

      expect(mixpanelStub.track_pageview).toHaveBeenCalledTimes(1)
      expect(mixpanelStub.track_pageview).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Dashboard',
          flow: 'dashboard'
        })
      )
    })

    it('includes custom data passed to pageLoad', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Login')

      initMixpanelBridge('test-token')
      window.digitalData!.pageLoad({ step: 'verify_otp' })

      expect(mixpanelStub.track_pageview).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Login',
          step: 'verify_otp'
        })
      )
    })
  })

  describe('EventTracked forwarding', () => {
    it('forwards tracked events to mixpanel.track', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      window.digitalData!.trackEvent('cta_click', { cta_id: 'sign-up' })

      expect(mixpanelStub.track).toHaveBeenCalledTimes(1)
      expect(mixpanelStub.track).toHaveBeenCalledWith('cta_click', expect.objectContaining({ cta_id: 'sign-up' }))
    })

    it('enriches event payload with analytics-scoped page data', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Dashboard')
      window.digitalData!.page.set('flow', 'dashboard')

      initMixpanelBridge('test-token')
      window.digitalData!.trackEvent('cta_click', { cta_id: 'nav' })

      expect(mixpanelStub.track).toHaveBeenCalledWith(
        'cta_click',
        expect.objectContaining({
          cta_id: 'nav',
          page_name: 'Dashboard',
          page_flow: 'dashboard'
        })
      )
    })

    it('enriches event payload with analytics-scoped user data', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('authenticated', true, ['default', 'analytics'])
      window.digitalData!.user.set('identity_assurance_level', 2, ['default', 'analytics'])

      initMixpanelBridge('test-token')
      window.digitalData!.trackEvent('otp_result', { status: 'success' })

      expect(mixpanelStub.track).toHaveBeenCalledWith(
        'otp_result',
        expect.objectContaining({
          status: 'success',
          user_authenticated: true,
          user_identity_assurance_level: 2
        })
      )
    })

    it('does NOT include user data that lacks analytics scope', () => {
      new DataLayer('digitalData')
      // Set user data with only 'default' scope (no analytics access)
      window.digitalData!.user.set('email', 'private@example.com')

      initMixpanelBridge('test-token')
      window.digitalData!.trackEvent('otp_result')

      const payload = mixpanelStub.track.mock.calls[0]![1] as Record<string, unknown>
      expect(payload).not.toHaveProperty('user_email')
    })

    it('forwards events without eventData', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      window.digitalData!.trackEvent('logout')

      expect(mixpanelStub.track).toHaveBeenCalledWith('logout', expect.any(Object))
    })
  })

  describe('user identity sync', () => {
    it('enriches subsequent events with user data set after bridge init', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      // User authenticates after bridge is running
      window.digitalData!.user.set('authenticated', true, ['default', 'analytics'])

      window.digitalData!.trackEvent('household_result', { status: 'success' })

      expect(mixpanelStub.track).toHaveBeenCalledWith(
        'household_result',
        expect.objectContaining({ user_authenticated: true })
      )
    })
  })

  describe('SDK compatibility', () => {
    it('disables session replay by default to avoid PII capture', () => {
      new DataLayer('digitalData')
      initMixpanelBridge('test-token')

      expect(mixpanelStub.init).toHaveBeenCalledWith(
        'test-token',
        expect.objectContaining({ record_sessions_percent: 0 })
      )
    })

    it('falls back to mp.track when track_pageview is not available', () => {
      // Simulate an older Mixpanel build without track_pageview
      ;(window as unknown as Record<string, unknown>).mixpanel = {
        init: vi.fn(),
        track: vi.fn()
      }
      const mp = (window as unknown as Record<string, unknown>).mixpanel as {
        init: ReturnType<typeof vi.fn>
        track: ReturnType<typeof vi.fn>
      }

      new DataLayer('digitalData')
      initMixpanelBridge('test-token')
      window.digitalData!.pageLoad({ name: 'Dashboard' })

      expect(mp.track).toHaveBeenCalledWith('page_view', expect.objectContaining({ name: 'Dashboard' }))
    })
  })
})
