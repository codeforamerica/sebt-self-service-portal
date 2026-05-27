import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { initAmplitudeBridge } from './amplitude-bridge'
import { DataLayer } from './data-layer'

function createAmplitudeStub() {
  return {
    init: vi.fn(),
    track: vi.fn()
  }
}

describe('initAmplitudeBridge', () => {
  let amplitudeStub: ReturnType<typeof createAmplitudeStub>

  beforeEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
    amplitudeStub = createAmplitudeStub()
  })

  afterEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
  })

  describe('initialization', () => {
    it('calls amplitude.init with the provided API key', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      expect(amplitudeStub.init).toHaveBeenCalledWith('test-key', expect.any(Object))
    })

    it('attaches listeners immediately when data layer is already initialized', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      window.digitalData!.trackEvent('cta_click', { cta_id: 'sign-up' })

      expect(amplitudeStub.track).toHaveBeenCalledTimes(1)
    })

    it('waits for DataLayer:Initialized when data layer is not yet ready', () => {
      initAmplitudeBridge('test-key', amplitudeStub)

      expect(amplitudeStub.track).not.toHaveBeenCalled()

      new DataLayer('digitalData')
      window.digitalData!.trackEvent('deferred_event')

      expect(amplitudeStub.track).toHaveBeenCalledTimes(1)
    })

    it('returns a teardown function that removes listeners', () => {
      new DataLayer('digitalData')
      const teardown = initAmplitudeBridge('test-key', amplitudeStub)

      teardown()

      window.digitalData!.trackEvent('click', { target: 'button' })

      expect(amplitudeStub.track).not.toHaveBeenCalled()
    })
  })

  describe('EventTracked forwarding', () => {
    it('forwards tracked events to amplitude.track', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      window.digitalData!.trackEvent('cta_click', { cta_id: 'sign-up' })

      expect(amplitudeStub.track).toHaveBeenCalledWith('cta_click', { cta_id: 'sign-up' })
    })

    it('forwards events without eventData', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      window.digitalData!.trackEvent('logout')

      expect(amplitudeStub.track).toHaveBeenCalledWith('logout', {})
    })

    it('ignores EventTracked events with an empty detail object', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      const eventTrackedEvent = window.digitalData!.eventTypes.EVENT_TRACKED!
      document.dispatchEvent(new CustomEvent(eventTrackedEvent, { detail: {} }))

      expect(amplitudeStub.track).not.toHaveBeenCalled()
    })
  })

  describe('PageViewed forwarding', () => {
    it('forwards pageLoad to amplitude.track with page_load and eventData', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      window.digitalData!.pageLoad({ name: 'Dashboard' })

      expect(amplitudeStub.track).toHaveBeenCalledWith(
        'page_load',
        expect.objectContaining({ name: 'Dashboard' })
      )
    })
  })

  describe('error handling', () => {
    it('logs a warning and returns a no-op teardown when amplitude.init throws', () => {
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
      amplitudeStub.init.mockImplementation(() => {
        throw new Error('boom')
      })
      new DataLayer('digitalData')

      const teardown = initAmplitudeBridge('test-key', amplitudeStub)

      expect(warnSpy).toHaveBeenCalledWith(
        expect.stringContaining('amplitude.init failed'),
        expect.any(Error)
      )
      // The bridge must not attach when init throws — no tracking should flow.
      window.digitalData!.trackEvent('cta_click')
      expect(amplitudeStub.track).not.toHaveBeenCalled()
      // And teardown must be callable without throwing.
      expect(() => teardown()).not.toThrow()

      warnSpy.mockRestore()
    })

    it('logs a warning when DataLayer:Initialized fires without a rootElement', () => {
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
      initAmplitudeBridge('test-key', amplitudeStub)

      document.dispatchEvent(new CustomEvent('DataLayer:Initialized', { detail: {} }))

      expect(warnSpy).toHaveBeenCalledWith(
        expect.stringContaining('without rootElement')
      )
      warnSpy.mockRestore()
    })
  })

  describe('privacy configuration', () => {
    it('disables default tracking and autocapture', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      expect(amplitudeStub.init).toHaveBeenCalledWith(
        'test-key',
        expect.objectContaining({
          defaultTracking: false,
          autocapture: false
        })
      )
    })

    it('disables cross-session identity storage', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      expect(amplitudeStub.init).toHaveBeenCalledWith(
        'test-key',
        expect.objectContaining({ identityStorage: 'none' })
      )
    })

    it('disables IP address capture', () => {
      new DataLayer('digitalData')
      initAmplitudeBridge('test-key', amplitudeStub)

      expect(amplitudeStub.init).toHaveBeenCalledWith(
        'test-key',
        expect.objectContaining({
          trackingOptions: expect.objectContaining({ ipAddress: false })
        })
      )
    })
  })
})
