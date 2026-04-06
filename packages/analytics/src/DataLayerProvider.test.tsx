import { act, render } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DataLayer } from './data-layer'
import { DataLayerProvider } from './DataLayerProvider'
import type { RoutePageContextMap } from './DataLayerProvider'

let mockPathname = '/dashboard'
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname
}))

// jsdom does not implement requestAnimationFrame; stub it to flush synchronously
vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
  cb(0)
  return 0
})
vi.stubGlobal('cancelAnimationFrame', vi.fn())

const routes: RoutePageContextMap = {
  '/dashboard': { name: 'Dashboard', flow: 'dashboard', step: 'dashboard' },
  '/login': { name: 'Login', flow: 'auth', step: 'login' }
}

describe('PageTracker (via DataLayerProvider)', () => {
  beforeEach(() => {
    delete (window as unknown as Record<string, unknown>).digitalData
    mockPathname = '/dashboard'
  })

  it('sets correct context for a matched route', () => {
    new DataLayer('digitalData')

    render(
      <DataLayerProvider application="test" routes={routes}>
        <div />
      </DataLayerProvider>
    )

    expect(window.digitalData!.get('page.name')).toBe('Dashboard')
    expect(window.digitalData!.get('page.flow')).toBe('dashboard')
    expect(window.digitalData!.get('page.step')).toBe('dashboard')
  })

  it('falls back to document.title for an unmatched route', () => {
    mockPathname = '/unknown'
    document.title = 'Fallback Title'
    new DataLayer('digitalData')

    render(
      <DataLayerProvider application="test" routes={routes}>
        <div />
      </DataLayerProvider>
    )

    expect(window.digitalData!.get('page.name')).toBe('Fallback Title')
  })

  it('fires a page_load event on mount', () => {
    new DataLayer('digitalData')

    render(
      <DataLayerProvider application="test" routes={routes}>
        <div />
      </DataLayerProvider>
    )

    expect(window.digitalData!.event.length).toBeGreaterThanOrEqual(1)
    const pageLoadEvent = window.digitalData!.event.find((e) => e.eventName === 'page_load')
    expect(pageLoadEvent).toBeDefined()
    expect(pageLoadEvent!.eventData).toEqual(
      expect.objectContaining({ name: 'Dashboard', flow: 'dashboard' })
    )
  })

  it('updates context when pathname changes', () => {
    new DataLayer('digitalData')

    const { rerender } = render(
      <DataLayerProvider application="test" routes={routes}>
        <div />
      </DataLayerProvider>
    )

    expect(window.digitalData!.get('page.name')).toBe('Dashboard')

    // Simulate navigation
    mockPathname = '/login'
    act(() => {
      rerender(
        <DataLayerProvider application="test" routes={routes}>
          <div />
        </DataLayerProvider>
      )
    })

    expect(window.digitalData!.get('page.name')).toBe('Login')
    expect(window.digitalData!.get('page.flow')).toBe('auth')
    expect(window.digitalData!.get('page.step')).toBe('login')
  })

  it('does not fire extra page_load when routes prop reference changes', () => {
    new DataLayer('digitalData')

    const { rerender } = render(
      <DataLayerProvider application="test" routes={{ ...routes }}>
        <div />
      </DataLayerProvider>
    )

    const countAfterMount = window.digitalData!.event.filter(
      (e) => e.eventName === 'page_load'
    ).length

    // Re-render with a new routes object reference but same pathname
    act(() => {
      rerender(
        <DataLayerProvider application="test" routes={{ ...routes }}>
          <div />
        </DataLayerProvider>
      )
    })

    const countAfterRerender = window.digitalData!.event.filter(
      (e) => e.eventName === 'page_load'
    ).length

    expect(countAfterRerender).toBe(countAfterMount)
  })
})
