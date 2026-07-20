import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import type { SmartySuggestion } from './types'

const SMARTY_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

// Set the embedded key so the hook is enabled
beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
  process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY = 'test-embedded-key'
})

afterEach(() => {
  vi.useRealTimers()
  delete process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
})

// Lazy import so env var is set before module loads
async function importHook() {
  const mod = await import('./useAddressAutocomplete')
  return mod.useAddressAutocomplete
}

function makeSuggestion(overrides: Partial<SmartySuggestion> = {}): SmartySuggestion {
  return {
    street_line: '123 Main St',
    secondary: '',
    city: 'Washington',
    state: 'DC',
    zipcode: '20001',
    entries: 0,
    ...overrides
  }
}

describe('useAddressAutocomplete', () => {
  it('returns empty suggestions when search is shorter than 3 characters', async () => {
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result } = renderHook(() =>
      useAddressAutocomplete({ search: 'ab', stateCode: 'dc', onSelect })
    )

    await act(() => vi.advanceTimersByTime(300))

    expect(result.current.suggestions).toEqual([])
    expect(result.current.isOpen).toBe(false)
  })

  it('fetches suggestions after debounce delay when search >= 3 chars', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    // Start with empty value so the initial-render skip doesn't suppress the
    // real search. Then rerender with a long-enough value to simulate user typing.
    const { result, rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    // Before debounce: no suggestions
    expect(result.current.suggestions).toEqual([])

    // After debounce: suggestions appear
    await act(() => vi.advanceTimersByTime(300))

    await waitFor(() => {
      expect(result.current.suggestions).toHaveLength(1)
      expect(result.current.isOpen).toBe(true)
    })
  })

  it('debounces rapid input changes', async () => {
    let fetchCount = 0
    server.use(
      http.get(SMARTY_URL, () => {
        fetchCount++
        return HttpResponse.json({ suggestions: [makeSuggestion()] })
      })
    )
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect }),
      { initialProps: { search: '123' } }
    )

    // Rapid changes within debounce window
    await act(() => vi.advanceTimersByTime(100))
    rerender({ search: '123 M' })
    await act(() => vi.advanceTimersByTime(100))
    rerender({ search: '123 Ma' })
    await act(() => vi.advanceTimersByTime(100))
    rerender({ search: '123 Mai' })

    // Wait for debounce to fire
    await act(() => vi.advanceTimersByTime(300))

    await waitFor(() => {
      // Only one fetch should have been made (for the final value)
      expect(fetchCount).toBe(1)
    })
  })

  it('passes prefer_states based on stateCode', async () => {
    let capturedUrl = ''
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ suggestions: [] })
      })
    )
    const useAddressAutocomplete = await importHook()

    // Start with empty value; rerender to simulate typing past the initial-render skip
    const { rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect: vi.fn() }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    await act(() => vi.advanceTimersByTime(300))

    await waitFor(() => {
      expect(capturedUrl).toBeTruthy()
      const url = new URL(capturedUrl)
      expect(url.searchParams.get('prefer_states')).toBe('DC;VA;MD')
    })
  })

  it('calls onSelect with address fields when a single-entry suggestion is selected', async () => {
    const suggestion = makeSuggestion({ secondary: 'Apt 2' })
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [suggestion] })))
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result, rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.suggestions).toHaveLength(1))

    act(() => result.current.selectSuggestion(0))

    expect(onSelect).toHaveBeenCalledWith({
      streetLine1: '123 Main St',
      streetLine2: 'Apt 2',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001'
    })
    expect(result.current.isOpen).toBe(false)
  })

  it('fetches unit suggestions when a multi-entry suggestion is selected', async () => {
    const multiUnit = makeSuggestion({ secondary: 'Apt', entries: 5 })
    const unitSuggestions = [
      makeSuggestion({ secondary: 'Apt 1' }),
      makeSuggestion({ secondary: 'Apt 2' }),
      makeSuggestion({ secondary: 'Apt 3' })
    ]

    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        const url = new URL(request.url)
        if (url.searchParams.get('selected')) {
          return HttpResponse.json({ suggestions: unitSuggestions })
        }
        return HttpResponse.json({ suggestions: [multiUnit] })
      })
    )
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result, rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.suggestions).toHaveLength(1))

    // Select the multi-unit suggestion
    await act(() => result.current.selectSuggestion(0))

    // onSelect should NOT have been called yet
    expect(onSelect).not.toHaveBeenCalled()

    // Unit suggestions should now be showing
    await waitFor(() => {
      expect(result.current.suggestions).toHaveLength(3)
      expect(result.current.suggestions[0]?.secondary).toBe('Apt 1')
      expect(result.current.isOpen).toBe(true)
    })
  })

  it('closes suggestions on dismiss', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))
    const useAddressAutocomplete = await importHook()

    const { result, rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect: vi.fn() }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.isOpen).toBe(true))

    act(() => result.current.dismiss())

    expect(result.current.isOpen).toBe(false)
    expect(result.current.suggestions).toEqual([])
  })

  it('clears suggestions when search drops below 3 characters', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))
    const useAddressAutocomplete = await importHook()

    // Start empty so the initial-render skip doesn't suppress the real search
    const { result, rerender } = renderHook(
      ({ search }) => useAddressAutocomplete({ search, stateCode: 'dc', onSelect: vi.fn() }),
      { initialProps: { search: '' } }
    )

    rerender({ search: '123 Main' })

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.suggestions).toHaveLength(1))

    // User clears the input
    rerender({ search: '12' })
    await act(() => vi.advanceTimersByTime(300))

    expect(result.current.suggestions).toEqual([])
    expect(result.current.isOpen).toBe(false)
  })
})
