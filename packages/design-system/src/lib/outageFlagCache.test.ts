import { afterEach, describe, expect, it, vi } from 'vitest'

import { createOutageFlagCache } from './outageFlagCache'

describe('createOutageFlagCache', () => {
  const cache = createOutageFlagCache('test_outage_flag')

  afterEach(() => {
    vi.restoreAllMocks()
    window.sessionStorage.clear()
  })

  it('returns null when nothing is cached', () => {
    expect(cache.read()).toBeNull()
  })

  it('round-trips true and false values', () => {
    cache.write(true)
    expect(cache.read()).toBe(true)

    cache.write(false)
    expect(cache.read()).toBe(false)
  })

  it('clears cached values', () => {
    cache.write(true)
    cache.clear()
    expect(cache.read()).toBeNull()
  })

  it('returns null for a value it did not write', () => {
    window.sessionStorage.setItem('test_outage_flag', 'maybe')
    expect(cache.read()).toBeNull()
  })

  // Each surface caches its own outage state; an outage on one says nothing about the other.
  it('isolates caches created with different keys', () => {
    const portal = createOutageFlagCache('sebt_outage_page_enabled')
    const checker = createOutageFlagCache('sebt_checker_outage_page_enabled')

    portal.write(true)

    expect(portal.read()).toBe(true)
    expect(checker.read()).toBeNull()
  })

  // sessionStorage throws in private browsing and some strict environments. A cache miss only
  // costs a frame of unblocked content, so it must never surface as an error.
  it('degrades to null when sessionStorage throws on read', () => {
    vi.spyOn(window.sessionStorage, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError')
    })

    expect(cache.read()).toBeNull()
  })

  it('swallows a throwing write', () => {
    vi.spyOn(window.sessionStorage, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })

    expect(() => cache.write(true)).not.toThrow()
  })

  it('swallows a throwing clear', () => {
    vi.spyOn(window.sessionStorage, 'removeItem').mockImplementation(() => {
      throw new Error('SecurityError')
    })

    expect(() => cache.clear()).not.toThrow()
  })
})
