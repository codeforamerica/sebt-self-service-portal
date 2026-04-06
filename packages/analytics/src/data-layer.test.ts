import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DataLayer } from './data-layer'

describe('DataLayer', () => {
  beforeEach(() => {
    // Clean up any previous instance from window
    delete (window as unknown as Record<string, unknown>).digitalData
  })

  // ── Constructor ──

  describe('constructor', () => {
    it('binds the data structure to window[root]', () => {
      new DataLayer('digitalData')

      expect(window.digitalData!).toBeDefined()
      expect(window.digitalData!.initialized).toBe(true)
    })

    it('initializes the canonical data structure', () => {
      new DataLayer('digitalData')

      // Sub-objects exist with their own nested structures
      expect(window.digitalData!.page.category).toBeDefined()
      expect(window.digitalData!.page.attribute).toBeDefined()
      expect(window.digitalData!.user.profile).toBeDefined()
      expect(window.digitalData!.event).toEqual([])
      expect(window.digitalData!.privacy).toEqual(expect.objectContaining({ accessCategories: [] }))
    })

    it('emits DataLayer:Initialized event on document', () => {
      const handler = vi.fn()
      document.addEventListener('DataLayer:Initialized', handler)

      new DataLayer('digitalData')

      expect(handler).toHaveBeenCalledTimes(1)
      const event = handler.mock.calls[0]![0] as CustomEvent
      expect(event.detail).toEqual(expect.objectContaining({ rootElement: 'digitalData' }))

      document.removeEventListener('DataLayer:Initialized', handler)
    })

    it('parses bootstrap JSON to populate initial state', () => {
      new DataLayer('digitalData', { text: JSON.stringify({ page: { name: 'Home' } }) })

      expect(window.digitalData!.get('page.name')).toBe('Home')
    })

    it('handles malformed bootstrap JSON gracefully', () => {
      new DataLayer('digitalData', { text: 'not valid json{{{' })

      // Should still initialize successfully
      expect(window.digitalData!).toBeDefined()
      expect(window.digitalData!.initialized).toBe(true)
    })

    it('rejects prototype pollution in bootstrap data', () => {
      const malicious = JSON.stringify({ __proto__: { polluted: true } })
      new DataLayer('digitalData', { text: malicious })

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect(({} as any).polluted).toBeUndefined()
    })

    it('rejects prototype pollution via setPath', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('__proto__.polluted', true)

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect(({} as any).polluted).toBeUndefined()
    })

    it('re-enforces canonical shape when bootstrap overwrites structural nodes', () => {
      new DataLayer('digitalData', { text: JSON.stringify({ page: 'oops', user: 42 }) })

      // Should still have object sub-structures despite bootstrap corruption
      expect(typeof window.digitalData!.page).toBe('object')
      expect(typeof window.digitalData!.user).toBe('object')
      expect(window.digitalData!.page.category).toBeDefined()
      expect(window.digitalData!.user.profile).toBeDefined()
    })

    it('exposes eventTypes on the root object', () => {
      new DataLayer('digitalData')

      expect(window.digitalData!.eventTypes).toBeDefined()
      expect(window.digitalData!.eventTypes.INITIALIZED).toBe('DataLayer:Initialized')
      expect(window.digitalData!.eventTypes.EVENT_TRACKED).toBe('digitalData:EventTracked')
    })
  })

  // ── get (read) ──

  describe('get', () => {
    it('reads a value by dot path', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Dashboard')

      expect(window.digitalData!.get('page.name')).toBe('Dashboard')
    })

    it('returns defaultValue when path does not exist', () => {
      new DataLayer('digitalData')

      expect(window.digitalData!.get('page.missing', undefined, 'fallback')).toBe('fallback')
    })

    it('returns undefined when path does not exist and no default', () => {
      new DataLayer('digitalData')

      expect(window.digitalData!.get('page.nonexistent')).toBeUndefined()
    })

    it('respects scope — denies access when scope does not match', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('email', 'test@example.com', 'default')

      // Requesting with a different scope should not return the value
      expect(window.digitalData!.get('user.email', 'analytics')).toBeUndefined()
    })

    it('grants access when scope matches', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('email', 'test@example.com', ['default', 'analytics'])

      expect(window.digitalData!.get('user.email', 'analytics')).toBe('test@example.com')
    })

    it('grants access when no scope is set on element (publicly readable)', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Home')

      expect(window.digitalData!.get('page.name', 'analytics')).toBe('Home')
    })

    it('inherits scope from parent when element has no scope', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('role', 'admin')
      // user data gets 'default' scope automatically — child should inherit

      expect(window.digitalData!.get('user.role', 'default')).toBe('admin')
      expect(window.digitalData!.get('user.role', 'analytics')).toBeUndefined()
    })
  })

  // ── page.set ──

  describe('page.set', () => {
    it('sets a page-level data element', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Dashboard')

      expect(window.digitalData!.get('page.name')).toBe('Dashboard')
    })

    it('emits digitalData:PageElementSet event', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:PageElementSet', handler)

      window.digitalData!.page.set('name', 'Home')

      expect(handler).toHaveBeenCalledTimes(1)
      document.removeEventListener('digitalData:PageElementSet', handler)
    })

    it('sets value with optional scope', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('secret', 'hidden', 'internal')

      expect(window.digitalData!.get('page.secret', 'internal')).toBe('hidden')
      expect(window.digitalData!.get('page.secret', 'analytics')).toBeUndefined()
    })
  })

  // ── page.category.set ──

  describe('page.category.set', () => {
    it('sets a page category element', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.category.set('primaryCategory', 'benefits')

      expect(window.digitalData!.get('page.category.primaryCategory')).toBe('benefits')
    })

    it('emits digitalData:PageCategorySet event', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:PageCategorySet', handler)

      window.digitalData!.page.category.set('type', 'landing')

      expect(handler).toHaveBeenCalledTimes(1)
      document.removeEventListener('digitalData:PageCategorySet', handler)
    })
  })

  // ── page.attribute.set ──

  describe('page.attribute.set', () => {
    it('sets a page attribute element', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.attribute.set('language', 'en')

      expect(window.digitalData!.get('page.attribute.language')).toBe('en')
    })

    it('emits digitalData:PageAttributeSet event', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:PageAttributeSet', handler)

      window.digitalData!.page.attribute.set('language', 'en')

      expect(handler).toHaveBeenCalledTimes(1)
      document.removeEventListener('digitalData:PageAttributeSet', handler)
    })
  })

  // ── user.set ──

  describe('user.set', () => {
    it('sets a user-level data element', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('isAuthenticated', true)

      expect(window.digitalData!.get('user.isAuthenticated', 'default')).toBe(true)
    })

    it('automatically includes "default" scope even when none provided', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('isAuthenticated', true)

      // Should be readable with 'default' scope
      expect(window.digitalData!.get('user.isAuthenticated', 'default')).toBe(true)
      // Should NOT be readable with other scopes (user data is private by default)
      expect(window.digitalData!.get('user.isAuthenticated', 'analytics')).toBeUndefined()
    })

    it('preserves additional scopes alongside "default"', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('isAuthenticated', true, ['default', 'analytics'])

      expect(window.digitalData!.get('user.isAuthenticated', 'analytics')).toBe(true)
    })

    it('emits digitalData:UserElementSet event', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:UserElementSet', handler)

      window.digitalData!.user.set('isAuthenticated', true)

      expect(handler).toHaveBeenCalledTimes(1)
      document.removeEventListener('digitalData:UserElementSet', handler)
    })
  })

  // ── user.profile.set ──

  describe('user.profile.set', () => {
    it('sets a user profile element', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.profile.set('firstName', 'Jane')

      expect(window.digitalData!.get('user.profile.firstName', 'default')).toBe('Jane')
    })

    it('automatically includes "default" scope', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.profile.set('firstName', 'Jane')

      expect(window.digitalData!.get('user.profile.firstName', 'default')).toBe('Jane')
      expect(window.digitalData!.get('user.profile.firstName', 'analytics')).toBeUndefined()
    })

    it('emits digitalData:UserProfileSet event', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:UserProfileSet', handler)

      window.digitalData!.user.profile.set('firstName', 'Jane')

      expect(handler).toHaveBeenCalledTimes(1)
      document.removeEventListener('digitalData:UserProfileSet', handler)
    })
  })

  // ── trackEvent ──

  describe('trackEvent', () => {
    it('appends an event object to the event array', () => {
      new DataLayer('digitalData')
      window.digitalData!.trackEvent('click', { target: 'cta-button' })

      expect(window.digitalData!.event).toHaveLength(1)
      expect(window.digitalData!.event[0]).toEqual(
        expect.objectContaining({
          eventName: 'click',
          eventData: { target: 'cta-button' }
        })
      )
    })

    it('includes a timestamp on each event', () => {
      new DataLayer('digitalData')
      const before = Date.now()
      window.digitalData!.trackEvent('pageView')
      const after = Date.now()

      expect(window.digitalData!.event[0]!.timeStamp).toBeGreaterThanOrEqual(before)
      expect(window.digitalData!.event[0]!.timeStamp).toBeLessThanOrEqual(after)
    })

    it('includes scope array on each event', () => {
      new DataLayer('digitalData')
      window.digitalData!.trackEvent('pageView')

      expect(Array.isArray(window.digitalData!.event[0]!.scope)).toBe(true)
    })

    it('works without eventData', () => {
      new DataLayer('digitalData')
      window.digitalData!.trackEvent('logout')

      expect(window.digitalData!.event[0]!.eventName).toBe('logout')
      expect(window.digitalData!.event[0]!.eventData).toEqual({})
    })

    it('emits digitalData:EventTracked CustomEvent on document', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:EventTracked', handler)

      window.digitalData!.trackEvent('click', { target: 'nav-link' })

      expect(handler).toHaveBeenCalledTimes(1)
      const event = handler.mock.calls[0]![0] as CustomEvent
      expect(event.detail).toEqual(
        expect.objectContaining({
          eventName: 'click',
          eventData: { target: 'nav-link' }
        })
      )

      document.removeEventListener('digitalData:EventTracked', handler)
    })
  })

  // ── pageLoad ──

  describe('pageLoad', () => {
    it('pushes a page_load event to event[] with analytics scope', () => {
      new DataLayer('digitalData')
      window.digitalData!.pageLoad()

      expect(window.digitalData!.event).toHaveLength(1)
      expect(window.digitalData!.event[0]).toEqual(
        expect.objectContaining({
          eventName: 'page_load',
          scope: ['analytics']
        })
      )
    })

    it('includes a timestamp on the event', () => {
      new DataLayer('digitalData')
      const before = Date.now()
      window.digitalData!.pageLoad()
      const after = Date.now()

      expect(window.digitalData!.event[0]!.timeStamp).toBeGreaterThanOrEqual(before)
      expect(window.digitalData!.event[0]!.timeStamp).toBeLessThanOrEqual(after)
    })

    it('emits digitalData:PageViewed CustomEvent on document', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:PageViewed', handler)

      window.digitalData!.pageLoad()

      expect(handler).toHaveBeenCalledTimes(1)
      const event = handler.mock.calls[0]![0] as CustomEvent
      expect(event.detail).toHaveProperty('eventData')
      expect(event.detail).toHaveProperty('data')
      expect(event.detail).toHaveProperty('timeStamp')
      expect(event.detail).toHaveProperty('scope', ['analytics'])

      document.removeEventListener('digitalData:PageViewed', handler)
    })

    it('does NOT emit digitalData:EventTracked', () => {
      new DataLayer('digitalData')
      const handler = vi.fn()
      document.addEventListener('digitalData:EventTracked', handler)

      window.digitalData!.pageLoad()

      expect(handler).not.toHaveBeenCalled()

      document.removeEventListener('digitalData:EventTracked', handler)
    })

    it('includes current page context in event data', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Dashboard')
      window.digitalData!.page.set('flow', 'dashboard')

      window.digitalData!.pageLoad()

      const event = window.digitalData!.event[0]!
      expect(event.eventData).toEqual(
        expect.objectContaining({
          name: 'Dashboard',
          flow: 'dashboard'
        })
      )
    })

    it('merges provided data with current page context', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Login')
      window.digitalData!.page.set('flow', 'auth')

      window.digitalData!.pageLoad({ step: 'verify_otp', custom: 'value' })

      const event = window.digitalData!.event[0]!
      expect(event.eventData).toEqual(
        expect.objectContaining({
          name: 'Login',
          flow: 'auth',
          step: 'verify_otp',
          custom: 'value'
        })
      )
    })

    it('provided data overrides existing page context', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Old Name')

      window.digitalData!.pageLoad({ name: 'New Name' })

      expect(window.digitalData!.event[0]!.eventData).toEqual(
        expect.objectContaining({ name: 'New Name' })
      )
    })

    it('PageViewed detail.data matches the event data', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Home')

      const handler = vi.fn()
      document.addEventListener('digitalData:PageViewed', handler)

      window.digitalData!.pageLoad({ flow: 'dashboard' })

      const customEvent = handler.mock.calls[0]![0] as CustomEvent
      expect(customEvent.detail.data).toEqual(
        expect.objectContaining({
          name: 'Home',
          flow: 'dashboard'
        })
      )

      document.removeEventListener('digitalData:PageViewed', handler)
    })

    it('exposes PAGE_VIEWED in eventTypes', () => {
      new DataLayer('digitalData')

      expect(window.digitalData!.eventTypes.PAGE_VIEWED).toBe('digitalData:PageViewed')
    })
  })

  // ── Scope inheritance ──

  describe('scope inheritance', () => {
    it('page data is publicly readable by default (no scope restriction)', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Home')

      // Any scope should have access to unscoped page data
      expect(window.digitalData!.get('page.name', 'analytics')).toBe('Home')
      expect(window.digitalData!.get('page.name', 'default')).toBe('Home')
      expect(window.digitalData!.get('page.name')).toBe('Home')
    })

    it('user data is private by default (default scope only)', () => {
      new DataLayer('digitalData')
      window.digitalData!.user.set('id', '12345')

      expect(window.digitalData!.get('user.id', 'default')).toBe('12345')
      expect(window.digitalData!.get('user.id', 'analytics')).toBeUndefined()
    })

    it('scope array on set grants access to multiple scopes', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('campaign', 'summer2025', ['analytics', 'marketing'])

      expect(window.digitalData!.get('page.campaign', 'analytics')).toBe('summer2025')
      expect(window.digitalData!.get('page.campaign', 'marketing')).toBe('summer2025')
      expect(window.digitalData!.get('page.campaign', 'internal')).toBeUndefined()
    })
  })

  // ── Custom root name ──

  describe('custom root name', () => {
    it('supports a custom root name', () => {
      new DataLayer('myData')

      expect((window as unknown as Record<string, unknown>).myData).toBeDefined()

      delete (window as unknown as Record<string, unknown>).myData
    })

    it('namespaces events to the root name', () => {
      const handler = vi.fn()
      document.addEventListener('myData:EventTracked', handler)

      new DataLayer('myData')
      const root = (window as unknown as Record<string, unknown>).myData as NonNullable<
        typeof window.digitalData
      >
      root.trackEvent('test')

      expect(handler).toHaveBeenCalledTimes(1)

      document.removeEventListener('myData:EventTracked', handler)
      delete (window as unknown as Record<string, unknown>).myData
    })
  })
})
