import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DataLayer } from './data-layer'
import { useDataLayer } from './useDataLayer'

describe('useDataLayer', () => {
  beforeEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
  })

  it('returns stable references across re-renders', () => {
    new DataLayer('digitalData')
    const { result, rerender } = renderHook(() => useDataLayer())

    const first = result.current
    rerender()
    const second = result.current

    expect(first.trackEvent).toBe(second.trackEvent)
    expect(first.setPageData).toBe(second.setPageData)
    expect(first.setUserData).toBe(second.setUserData)
    expect(first.pageLoad).toBe(second.pageLoad)
    expect(first.get).toBe(second.get)
  })

  it('trackEvent delegates to window.digitalData.trackEvent', () => {
    new DataLayer('digitalData')
    const spy = vi.spyOn(window.digitalData!, 'trackEvent')

    const { result } = renderHook(() => useDataLayer())
    result.current.trackEvent('test_event', { key: 'value' })

    expect(spy).toHaveBeenCalledWith('test_event', { key: 'value' })
  })

  it('setPageData delegates to window.digitalData.page.set', () => {
    new DataLayer('digitalData')
    const spy = vi.spyOn(window.digitalData!.page, 'set')

    const { result } = renderHook(() => useDataLayer())
    result.current.setPageData('name', 'Dashboard')

    expect(spy).toHaveBeenCalledWith('name', 'Dashboard', undefined)
  })

  it('setUserData delegates to window.digitalData.user.set', () => {
    new DataLayer('digitalData')
    const spy = vi.spyOn(window.digitalData!.user, 'set')

    const { result } = renderHook(() => useDataLayer())
    result.current.setUserData('authenticated', true, ['default', 'analytics'])

    expect(spy).toHaveBeenCalledWith('authenticated', true, ['default', 'analytics'])
  })

  it('get delegates to window.digitalData.get', () => {
    new DataLayer('digitalData')
    window.digitalData!.page.set('name', 'Home')

    const { result } = renderHook(() => useDataLayer())
    const value = result.current.get('page.name')

    expect(value).toBe('Home')
  })

  it('pageLoad delegates to window.digitalData.pageLoad', () => {
    new DataLayer('digitalData')
    const spy = vi.spyOn(window.digitalData!, 'pageLoad')

    const { result } = renderHook(() => useDataLayer())
    result.current.pageLoad({ flow: 'auth' })

    expect(spy).toHaveBeenCalledWith({ flow: 'auth' })
  })

  it('no-ops when data layer is not initialized', () => {
    const { result } = renderHook(() => useDataLayer())

    // Should not throw
    expect(() => result.current.trackEvent('test')).not.toThrow()
    expect(() => result.current.setPageData('name', 'x')).not.toThrow()
    expect(() => result.current.setUserData('auth', true)).not.toThrow()
    expect(() => result.current.pageLoad()).not.toThrow()
    expect(result.current.get('page.name')).toBeUndefined()
  })
})
