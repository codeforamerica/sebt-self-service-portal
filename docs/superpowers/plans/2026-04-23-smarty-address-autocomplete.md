# Smarty Address Autocomplete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add type-ahead address autocomplete to the street address field in the change-address form, powered by the Smarty US Autocomplete Pro API, so users can select a verified address as they type instead of entering it manually.

**Architecture:** The autocomplete calls Smarty's US Autocomplete Pro API directly from the browser using an embeddable key (no backend changes). A custom `useAddressAutocomplete` hook manages debounced search, two-stage unit selection, and state preference. An `AddressAutocomplete` component renders a WCAG 2.1 AA combobox, replacing the plain `InputField` for street address line 1 in `AddressForm`. When `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY` is not configured, the form gracefully falls back to the plain input.

**Tech Stack:** React 19, TypeScript, Smarty US Autocomplete Pro API, USWDS 3.13 (utility classes), Vitest + React Testing Library + MSW

**TDD:** `docs/tdd/smarty-address-autocomplete.md`

---

## Design Decision: Custom Async Combobox (TDD Option B)

The TDD left open whether to extend USWDS `usa-combo-box` (Option A) or build a custom async combobox (Option B). **We choose Option B.**

Reason: USWDS `usa-combo-box` is designed for static option lists with client-side filtering. Its JS initializes on `.usa-combo-box` elements and controls the dropdown lifecycle. Adapting it for async API-driven suggestions would require overriding most of its behavior while fighting its event handlers. A custom combobox using USWDS utility classes for visual consistency and implementing the ARIA combobox pattern from scratch is cleaner and more maintainable.

## State Preference Configuration

Smarty's `prefer_states` parameter biases results toward preferred states. Each portal state defines a comma-delimited, prioritized list of state abbreviations (home state first):
- **DC:** `DC,VA,MD`
- **CO:** `CO`

This is defined as a constant in the autocomplete module, keyed by state code.

---

## File Structure

```
src/SEBT.Portal.Web/
  next.config.ts                                          # Modify: add NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
  src/
    env.ts                                                # Modify: add NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
    features/address/
      components/
        AddressAutocomplete/
          index.ts                                        # Create: public exports
          types.ts                                        # Create: Smarty API types, SelectedAddress
          smartyAutocompleteClient.ts                     # Create: fetch wrapper
          smartyAutocompleteClient.test.ts                # Create: client tests (MSW)
          useAddressAutocomplete.ts                       # Create: hook (debounce, search, two-stage)
          useAddressAutocomplete.test.ts                  # Create: hook tests (MSW + fake timers)
          AddressAutocomplete.tsx                         # Create: combobox component
          AddressAutocomplete.test.tsx                    # Create: component tests
          AddressAutocomplete.module.scss                 # Create: dropdown positioning styles
        AddressForm/
          AddressForm.tsx                                 # Modify: integrate AddressAutocomplete
          AddressForm.test.tsx                            # Modify: add autocomplete integration tests
  .env.example                                            # Modify: add NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
```

---

### Task 1: Environment Configuration

Wire up the `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY` env var so Next.js exposes it to the browser.

**Files:**
- Modify: `src/SEBT.Portal.Web/src/env.ts`
- Modify: `src/SEBT.Portal.Web/next.config.ts`
- Modify: `src/SEBT.Portal.Web/.env.example`

- [ ] **Step 1: Add the env var to `env.ts` validation**

In `src/SEBT.Portal.Web/src/env.ts`, add to the `client` section (after the existing Socure keys):

```typescript
    // Smarty US Autocomplete Pro embeddable key.
    // When set, the change-address form shows type-ahead suggestions.
    // Omit to disable autocomplete (users type addresses manually).
    NEXT_PUBLIC_SMARTY_EMBEDDED_KEY: z.string().min(1).optional(),
```

And add to the `runtimeEnv` section:

```typescript
    NEXT_PUBLIC_SMARTY_EMBEDDED_KEY: process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY,
```

- [ ] **Step 2: Expose the env var in `next.config.ts`**

In `src/SEBT.Portal.Web/next.config.ts`, add to the `env` block (alongside `NEXT_PUBLIC_STATE`):

```typescript
  env: {
    NEXT_PUBLIC_STATE: state,
    NEXT_PUBLIC_SMARTY_EMBEDDED_KEY: process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY || '',
  },
```

- [ ] **Step 3: Document in `.env.example`**

In `src/SEBT.Portal.Web/.env.example`, add at the end of the client-side section:

```bash
# Smarty US Autocomplete Pro embeddable key.
# Enables type-ahead address suggestions in the change-address form.
# Obtain from Smarty dashboard > API Keys > Embedded Key.
# Domain-restricted in Smarty dashboard — safe for client-side use.
# Leave empty to disable autocomplete (users type addresses manually).
NEXT_PUBLIC_SMARTY_EMBEDDED_KEY=
```

- [ ] **Step 4: Verify env validation still passes**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/env.ts --passWithNoTests
```

Expected: no test failures (env.ts has no dedicated test; we're verifying it doesn't break imports).

- [ ] **Step 5: Commit**

```bash
git add src/SEBT.Portal.Web/src/env.ts src/SEBT.Portal.Web/next.config.ts src/SEBT.Portal.Web/.env.example
git commit -m "feat: add NEXT_PUBLIC_SMARTY_EMBEDDED_KEY env var for address autocomplete"
```

---

### Task 2: Smarty API Types and Client

Create TypeScript types for the Smarty US Autocomplete Pro API and a fetch wrapper that silently degrades on errors.

**Files:**
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/types.ts`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.ts`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.test.ts`

- [ ] **Step 1: Create `types.ts`**

```typescript
// types.ts — Smarty US Autocomplete Pro API types and shared autocomplete types

/** A single suggestion from the Smarty US Autocomplete Pro API. */
export interface SmartySuggestion {
  street_line: string
  secondary: string
  city: string
  state: string
  zipcode: string
  /** Number of entries at this address. >1 means multi-unit building requiring secondary lookup. */
  entries: number
}

/** Raw API response from Smarty US Autocomplete Pro. */
export interface SmartyAutocompleteResponse {
  suggestions: SmartySuggestion[]
}

/** Address fields populated when the user selects a final suggestion. */
export interface SelectedAddress {
  streetLine1: string
  streetLine2: string
  city: string
  state: string
  zipcode: string
}

/** Per-state comma-delimited, prioritized list of state abbreviations for Smarty's prefer_states parameter. */
export const STATE_PREFER_STATES: Record<string, string> = {
  dc: 'DC,VA,MD',
  co: 'CO'
}
```

- [ ] **Step 2: Write the failing tests for the client**

Create `smartyAutocompleteClient.test.ts`:

```typescript
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import { server } from '@/mocks/server'

import { fetchAutocompleteSuggestions, formatSelected } from './smartyAutocompleteClient'

const SMARTY_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

describe('fetchAutocompleteSuggestions', () => {
  it('returns parsed suggestions for a valid search', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({
          suggestions: [
            {
              street_line: '123 Main St',
              secondary: '',
              city: 'Washington',
              state: 'DC',
              zipcode: '20001',
              entries: 0
            }
          ]
        })
      )
    )

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toHaveLength(1)
    expect(results[0]).toEqual({
      street_line: '123 Main St',
      secondary: '',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001',
      entries: 0
    })
  })

  it('passes prefer_states as a query parameter', async () => {
    let capturedUrl = ''
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ suggestions: [] })
      })
    )

    await fetchAutocompleteSuggestions({
      search: '123',
      key: 'test-key',
      preferStates: 'DC,VA,MD'
    })

    const url = new URL(capturedUrl)
    expect(url.searchParams.get('prefer_states')).toBe('DC,VA,MD')
  })

  it('passes selected parameter for secondary lookups', async () => {
    let capturedUrl = ''
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ suggestions: [] })
      })
    )

    await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key',
      selected: '123 Main St Apt (5) Washington DC 20001'
    })

    const url = new URL(capturedUrl)
    expect(url.searchParams.get('selected')).toBe('123 Main St Apt (5) Washington DC 20001')
  })

  it('returns empty array when API responds with non-OK status', async () => {
    server.use(http.get(SMARTY_URL, () => new HttpResponse(null, { status: 500 })))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('returns empty array on network error', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.error()))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('returns empty array when response has no suggestions field', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({})))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('supports AbortController cancellation', async () => {
    server.use(
      http.get(SMARTY_URL, async () => {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        return HttpResponse.json({ suggestions: [] })
      })
    )

    const controller = new AbortController()
    const promise = fetchAutocompleteSuggestions(
      { search: '123 Main', key: 'test-key' },
      controller.signal
    )
    controller.abort()

    const results = await promise
    expect(results).toEqual([])
  })
})

describe('formatSelected', () => {
  it('formats a suggestion with secondary into the selected parameter', () => {
    const result = formatSelected({
      street_line: '123 Main St',
      secondary: 'Apt',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001',
      entries: 5
    })

    expect(result).toBe('123 Main St Apt (5) Washington DC 20001')
  })

  it('formats a suggestion without secondary', () => {
    const result = formatSelected({
      street_line: '456 Oak Ave',
      secondary: '',
      city: 'Denver',
      state: 'CO',
      zipcode: '80202',
      entries: 3
    })

    expect(result).toBe('456 Oak Ave (3) Denver CO 80202')
  })
})
```

- [ ] **Step 3: Run tests to verify they fail**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.test.ts
```

Expected: FAIL (module not found)

- [ ] **Step 4: Implement `smartyAutocompleteClient.ts`**

```typescript
import type { SmartySuggestion, SmartyAutocompleteResponse } from './types'

const SMARTY_AUTOCOMPLETE_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

export interface SmartyAutocompleteParams {
  search: string
  key: string
  preferStates?: string
  selected?: string
}

/**
 * Fetches address suggestions from the Smarty US Autocomplete Pro API.
 * Returns an empty array on any error — autocomplete is an enhancement,
 * not a requirement. Manual entry always works.
 */
export async function fetchAutocompleteSuggestions(
  params: SmartyAutocompleteParams,
  signal?: AbortSignal
): Promise<SmartySuggestion[]> {
  try {
    const url = new URL(SMARTY_AUTOCOMPLETE_URL)
    url.searchParams.set('search', params.search)
    url.searchParams.set('key', params.key)
    if (params.preferStates) url.searchParams.set('prefer_states', params.preferStates)
    if (params.selected) url.searchParams.set('selected', params.selected)

    const response = await fetch(url.toString(), { signal })
    if (!response.ok) return []

    const data: SmartyAutocompleteResponse = await response.json()
    return data.suggestions ?? []
  } catch {
    return []
  }
}

/**
 * Formats a multi-unit suggestion into the `selected` parameter value
 * for Smarty's secondary (unit) lookup.
 *
 * Format: "{street_line} {secondary} ({entries}) {city} {state} {zipcode}"
 * When secondary is empty, it is omitted.
 */
export function formatSelected(suggestion: SmartySuggestion): string {
  const parts = [suggestion.street_line]
  if (suggestion.secondary) parts.push(suggestion.secondary)
  parts.push(`(${suggestion.entries})`)
  parts.push(suggestion.city)
  parts.push(suggestion.state)
  parts.push(suggestion.zipcode)
  return parts.join(' ')
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.test.ts
```

Expected: all tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/types.ts \
        src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.ts \
        src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/smartyAutocompleteClient.test.ts
git commit -m "feat: add Smarty Autocomplete Pro API types and client with silent error degradation"
```

---

### Task 3: useAddressAutocomplete Hook

The hook manages debounced search, two-stage unit selection, and provides suggestions to the component.

**Files:**
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/useAddressAutocomplete.ts`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/useAddressAutocomplete.test.ts`

- [ ] **Step 1: Write the failing hook tests**

Create `useAddressAutocomplete.test.ts`:

```typescript
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
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result } = renderHook(() =>
      useAddressAutocomplete({ search: '123 Main', stateCode: 'dc', onSelect })
    )

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

    renderHook(() =>
      useAddressAutocomplete({ search: '123 Main', stateCode: 'dc', onSelect: vi.fn() })
    )

    await act(() => vi.advanceTimersByTime(300))

    await waitFor(() => {
      expect(capturedUrl).toBeTruthy()
      const url = new URL(capturedUrl)
      expect(url.searchParams.get('prefer_states')).toBe('DC,VA,MD')
    })
  })

  it('calls onSelect with address fields when a single-entry suggestion is selected', async () => {
    const suggestion = makeSuggestion({ secondary: 'Apt 2' })
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [suggestion] })
      )
    )
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result } = renderHook(() =>
      useAddressAutocomplete({ search: '123 Main', stateCode: 'dc', onSelect })
    )

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

    let callCount = 0
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        callCount++
        const url = new URL(request.url)
        if (url.searchParams.get('selected')) {
          return HttpResponse.json({ suggestions: unitSuggestions })
        }
        return HttpResponse.json({ suggestions: [multiUnit] })
      })
    )
    const useAddressAutocomplete = await importHook()
    const onSelect = vi.fn()

    const { result } = renderHook(() =>
      useAddressAutocomplete({ search: '123 Main', stateCode: 'dc', onSelect })
    )

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.suggestions).toHaveLength(1))

    // Select the multi-unit suggestion
    await act(() => result.current.selectSuggestion(0))

    // onSelect should NOT have been called yet
    expect(onSelect).not.toHaveBeenCalled()

    // Unit suggestions should now be showing
    await waitFor(() => {
      expect(result.current.suggestions).toHaveLength(3)
      expect(result.current.suggestions[0].secondary).toBe('Apt 1')
      expect(result.current.isOpen).toBe(true)
    })
  })

  it('closes suggestions on dismiss', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )
    const useAddressAutocomplete = await importHook()

    const { result } = renderHook(() =>
      useAddressAutocomplete({ search: '123 Main', stateCode: 'dc', onSelect: vi.fn() })
    )

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.isOpen).toBe(true))

    act(() => result.current.dismiss())

    expect(result.current.isOpen).toBe(false)
    expect(result.current.suggestions).toEqual([])
  })

  it('clears suggestions when search drops below 3 characters', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )
    const useAddressAutocomplete = await importHook()

    const { result, rerender } = renderHook(
      ({ search }) =>
        useAddressAutocomplete({ search, stateCode: 'dc', onSelect: vi.fn() }),
      { initialProps: { search: '123 Main' } }
    )

    await act(() => vi.advanceTimersByTime(300))
    await waitFor(() => expect(result.current.suggestions).toHaveLength(1))

    // User clears the input
    rerender({ search: '12' })
    await act(() => vi.advanceTimersByTime(300))

    expect(result.current.suggestions).toEqual([])
    expect(result.current.isOpen).toBe(false)
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/useAddressAutocomplete.test.ts
```

Expected: FAIL (module not found)

- [ ] **Step 3: Implement `useAddressAutocomplete.ts`**

```typescript
import { useCallback, useEffect, useRef, useState } from 'react'

import { fetchAutocompleteSuggestions, formatSelected } from './smartyAutocompleteClient'
import type { SelectedAddress, SmartySuggestion } from './types'
import { STATE_PREFER_STATES } from './types'

const DEBOUNCE_MS = 300
const MIN_CHARS = 3

interface UseAddressAutocompleteOptions {
  /** Current value of the street address input */
  search: string
  /** Portal state code (e.g. 'dc', 'co') — determines prefer_states */
  stateCode: string
  /** Called when the user selects a final address (single entry or unit) */
  onSelect: (address: SelectedAddress) => void
}

interface UseAddressAutocompleteReturn {
  suggestions: SmartySuggestion[]
  isOpen: boolean
  isLoading: boolean
  selectSuggestion: (index: number) => void
  dismiss: () => void
  open: () => void
}

export function useAddressAutocomplete({
  search,
  stateCode,
  onSelect
}: UseAddressAutocompleteOptions): UseAddressAutocompleteReturn {
  const [suggestions, setSuggestions] = useState<SmartySuggestion[]>([])
  const [isOpen, setIsOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  const abortRef = useRef<AbortController | null>(null)
  const onSelectRef = useRef(onSelect)
  onSelectRef.current = onSelect

  // Track the original search term for secondary lookups
  const searchAtSelectionRef = useRef('')

  const key = process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY ?? ''
  const enabled = key.length > 0
  // eslint-disable-next-line security/detect-object-injection -- stateCode is typed StateCode
  const preferStates = STATE_PREFER_STATES[stateCode] ?? ''

  // Debounced primary search
  useEffect(() => {
    if (!enabled || search.length < MIN_CHARS) {
      setSuggestions([])
      setIsOpen(false)
      return
    }

    const timer = setTimeout(() => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller

      setIsLoading(true)
      fetchAutocompleteSuggestions(
        { search, key, preferStates },
        controller.signal
      ).then((results) => {
        if (!controller.signal.aborted) {
          setSuggestions(results)
          setIsOpen(results.length > 0)
          setIsLoading(false)
        }
      })
    }, DEBOUNCE_MS)

    return () => clearTimeout(timer)
  }, [search, enabled, key, preferStates])

  const selectSuggestion = useCallback(
    (index: number) => {
      const suggestion = suggestions[index]
      if (!suggestion) return

      if (suggestion.entries > 1) {
        // Multi-unit building: fetch individual units
        searchAtSelectionRef.current = search
        abortRef.current?.abort()
        const controller = new AbortController()
        abortRef.current = controller

        setIsLoading(true)
        fetchAutocompleteSuggestions(
          {
            search: searchAtSelectionRef.current,
            key,
            preferStates,
            selected: formatSelected(suggestion)
          },
          controller.signal
        ).then((unitResults) => {
          if (!controller.signal.aborted) {
            setSuggestions(unitResults)
            setIsLoading(false)
            // Keep isOpen true to show unit options
          }
        })
      } else {
        // Single address: select and close
        onSelectRef.current({
          streetLine1: suggestion.street_line,
          streetLine2: suggestion.secondary,
          city: suggestion.city,
          state: suggestion.state,
          zipcode: suggestion.zipcode
        })
        setSuggestions([])
        setIsOpen(false)
      }
    },
    [suggestions, search, key, preferStates]
  )

  const dismiss = useCallback(() => {
    abortRef.current?.abort()
    setSuggestions([])
    setIsOpen(false)
  }, [])

  const open = useCallback(() => {
    if (suggestions.length > 0) setIsOpen(true)
  }, [suggestions])

  // Abort on unmount
  useEffect(() => () => abortRef.current?.abort(), [])

  return { suggestions, isOpen, isLoading, selectSuggestion, dismiss, open }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/useAddressAutocomplete.test.ts
```

Expected: all tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/useAddressAutocomplete.ts \
        src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/useAddressAutocomplete.test.ts
git commit -m "feat: add useAddressAutocomplete hook with debounce and two-stage unit selection"
```

---

### Task 4: AddressAutocomplete Component

Build the combobox UI: input + dropdown listbox with full keyboard navigation and ARIA attributes.

**Files:**
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/AddressAutocomplete.module.scss`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/AddressAutocomplete.tsx`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/AddressAutocomplete.test.tsx`
- Create: `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/index.ts`

- [ ] **Step 1: Create the SCSS module for dropdown positioning**

Create `AddressAutocomplete.module.scss`:

```scss
.wrapper {
  position: relative;
}

.listbox {
  position: absolute;
  z-index: 400;
  top: 100%;
  left: 0;
  right: 0;
  max-height: 200px;
  overflow-y: auto;
  background-color: white;
  border: 1px solid #71767a; // USWDS base
  border-top: none;
  list-style: none;
  margin: 0;
  padding: 0;
}

.option {
  padding: 0.5rem 0.75rem;
  cursor: pointer;
  font-size: 1rem;

  &:hover,
  &[data-focused='true'] {
    background-color: #dfe1e2; // USWDS base-lighter
  }
}
```

- [ ] **Step 2: Write the failing component tests**

Create `AddressAutocomplete.test.tsx`:

```typescript
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import type { SmartySuggestion } from './types'

const SMARTY_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return { ...actual, getState: () => mockState }
})

function makeSuggestion(overrides: Partial<SmartySuggestion> = {}): SmartySuggestion {
  return {
    street_line: '123 Main St NW',
    secondary: '',
    city: 'Washington',
    state: 'DC',
    zipcode: '20001',
    entries: 0,
    ...overrides
  }
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
  process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY = 'test-embedded-key'
  mockState = 'dc'
})

afterEach(() => {
  vi.useRealTimers()
  delete process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
})

// Lazy import so env var is set before module evaluation
async function renderAutocomplete(props: Record<string, unknown> = {}) {
  const { AddressAutocomplete } = await import('./AddressAutocomplete')
  const defaultProps = {
    label: 'Street address',
    name: 'streetAddress1',
    value: '',
    onChange: vi.fn(),
    onSuggestionSelected: vi.fn(),
    isRequired: true
  }
  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
  const result = render(<AddressAutocomplete {...defaultProps} {...props} />)
  return { user, ...result, ...defaultProps }
}

describe('AddressAutocomplete', () => {
  // --- ARIA structure ---

  it('renders an input with combobox role', async () => {
    await renderAutocomplete()
    const input = screen.getByRole('combobox', { name: /street address/i })
    expect(input).toBeInTheDocument()
    expect(input).toHaveAttribute('aria-expanded', 'false')
    expect(input).toHaveAttribute('aria-autocomplete', 'list')
  })

  it('renders a listbox when suggestions are visible', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )

    const { user } = await renderAutocomplete()
    const input = screen.getByRole('combobox')

    await user.type(input, '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => {
      expect(screen.getByRole('listbox')).toBeInTheDocument()
      expect(input).toHaveAttribute('aria-expanded', 'true')
    })
  })

  it('does not render a listbox when there are no suggestions', async () => {
    server.use(
      http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [] }))
    )

    const { user } = await renderAutocomplete()
    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => {
      expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    })
  })

  // --- Suggestion selection ---

  it('calls onSuggestionSelected when a suggestion is clicked', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({
          suggestions: [makeSuggestion({ secondary: 'Apt 2' })]
        })
      )
    )

    const onSuggestionSelected = vi.fn()
    const { user } = await renderAutocomplete({ onSuggestionSelected })

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())

    await user.click(screen.getByRole('option'))

    expect(onSuggestionSelected).toHaveBeenCalledWith({
      streetLine1: '123 Main St NW',
      streetLine2: 'Apt 2',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001'
    })
  })

  // --- Keyboard navigation ---

  it('moves focus through suggestions with arrow keys', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({
          suggestions: [
            makeSuggestion({ street_line: '111 A St' }),
            makeSuggestion({ street_line: '222 B St' })
          ]
        })
      )
    )

    const { user } = await renderAutocomplete()
    const input = screen.getByRole('combobox')

    await user.type(input, '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(2))

    await user.keyboard('{ArrowDown}')
    const options = screen.getAllByRole('option')
    expect(options[0]).toHaveAttribute('data-focused', 'true')
    expect(input).toHaveAttribute('aria-activedescendant', options[0].id)

    await user.keyboard('{ArrowDown}')
    expect(options[1]).toHaveAttribute('data-focused', 'true')
  })

  it('selects the focused suggestion on Enter', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )

    const onSuggestionSelected = vi.fn()
    const { user } = await renderAutocomplete({ onSuggestionSelected })

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())

    await user.keyboard('{ArrowDown}{Enter}')

    expect(onSuggestionSelected).toHaveBeenCalledTimes(1)
  })

  it('closes suggestions on Escape', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({ suggestions: [makeSuggestion()] })
      )
    )

    const { user } = await renderAutocomplete()

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument())

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  // --- Screen reader ---

  it('announces suggestion count via live region', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({
          suggestions: [
            makeSuggestion({ street_line: '111 A St' }),
            makeSuggestion({ street_line: '222 B St' })
          ]
        })
      )
    )

    const { user } = await renderAutocomplete()

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => {
      const status = screen.getByRole('status')
      expect(status).toHaveTextContent(/2.*suggestion/i)
    })
  })

  // --- Error display ---

  it('passes error styling through when error prop is provided', async () => {
    await renderAutocomplete({ error: 'This field is required.' })

    expect(screen.getByText('This field is required.')).toBeInTheDocument()
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-invalid', 'true')
  })

  // --- Graceful degradation ---

  it('renders as a plain input when Smarty key is not configured', async () => {
    delete process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY

    // Re-import with no key
    vi.resetModules()
    const { AddressAutocomplete } = await import('./AddressAutocomplete')
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })

    render(
      <AddressAutocomplete
        label="Street address"
        name="streetAddress1"
        value=""
        onChange={vi.fn()}
        onSuggestionSelected={vi.fn()}
        isRequired
      />
    )

    const input = screen.getByRole('textbox', { name: /street address/i })
    expect(input).toBeInTheDocument()
    // Should NOT have combobox role when disabled
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })

  // --- Multi-unit two-stage flow ---

  it('shows unit suggestions after selecting a multi-entry building', async () => {
    const multiUnit = makeSuggestion({ secondary: 'Apt', entries: 5 })
    const units = [
      makeSuggestion({ secondary: 'Apt 1' }),
      makeSuggestion({ secondary: 'Apt 2' })
    ]

    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        const url = new URL(request.url)
        if (url.searchParams.get('selected')) {
          return HttpResponse.json({ suggestions: units })
        }
        return HttpResponse.json({ suggestions: [multiUnit] })
      })
    )

    const onSuggestionSelected = vi.fn()
    const { user } = await renderAutocomplete({ onSuggestionSelected })

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())

    // Click the multi-unit suggestion
    await user.click(screen.getByRole('option'))

    // Should NOT have called onSuggestionSelected yet
    expect(onSuggestionSelected).not.toHaveBeenCalled()

    // Should now show unit suggestions
    await waitFor(() => {
      const options = screen.getAllByRole('option')
      expect(options).toHaveLength(2)
    })

    // Select a unit
    await user.click(screen.getAllByRole('option')[1])

    expect(onSuggestionSelected).toHaveBeenCalledWith(
      expect.objectContaining({ streetLine2: 'Apt 2' })
    )
  })
})
```

- [ ] **Step 3: Run tests to verify they fail**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/AddressAutocomplete.test.tsx
```

Expected: FAIL (module not found)

- [ ] **Step 4: Implement `AddressAutocomplete.tsx`**

```tsx
'use client'

import { useId, useRef, useState, type ChangeEvent, type InputHTMLAttributes, type KeyboardEvent } from 'react'

import { getState } from '@sebt/design-system'

import type { SelectedAddress } from './types'
import { useAddressAutocomplete } from './useAddressAutocomplete'
import styles from './AddressAutocomplete.module.scss'

interface AddressAutocompleteProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'role'> {
  label: string
  name: string
  value: string
  onChange: (e: ChangeEvent<HTMLInputElement>) => void
  onSuggestionSelected: (address: SelectedAddress) => void
  error?: string
  hint?: string
  isRequired?: boolean
}

/**
 * Address input with Smarty autocomplete suggestions.
 * Falls back to a plain text input when NEXT_PUBLIC_SMARTY_EMBEDDED_KEY is not set.
 */
export function AddressAutocomplete({
  label,
  name,
  value,
  onChange,
  onSuggestionSelected,
  error,
  hint,
  isRequired,
  ...inputProps
}: AddressAutocompleteProps) {
  const baseId = useId()
  const inputId = `${baseId}-input`
  const listboxId = `${baseId}-listbox`
  const statusId = `${baseId}-status`
  const hintId = hint ? `${baseId}-hint` : undefined
  const errorId = error ? `${baseId}-error` : undefined

  const inputRef = useRef<HTMLInputElement>(null)
  const [activeIndex, setActiveIndex] = useState(-1)

  const smartyKey = process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY ?? ''
  const enabled = smartyKey.length > 0

  const autocomplete = useAddressAutocomplete({
    search: value,
    stateCode: getState(),
    onSelect: (address) => {
      onSuggestionSelected(address)
      setActiveIndex(-1)
    }
  })

  const { suggestions, isOpen, selectSuggestion, dismiss, open } = autocomplete

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!isOpen) {
      if (e.key === 'ArrowDown') {
        open()
        e.preventDefault()
      }
      return
    }

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        setActiveIndex((prev) => Math.min(prev + 1, suggestions.length - 1))
        break
      case 'ArrowUp':
        e.preventDefault()
        setActiveIndex((prev) => Math.max(prev - 1, -1))
        break
      case 'Enter':
        if (activeIndex >= 0) {
          e.preventDefault()
          selectSuggestion(activeIndex)
          setActiveIndex(-1)
        }
        break
      case 'Escape':
        e.preventDefault()
        dismiss()
        setActiveIndex(-1)
        break
    }
  }

  function handleSuggestionClick(index: number) {
    selectSuggestion(index)
    setActiveIndex(-1)
    inputRef.current?.focus()
  }

  // Build aria-describedby from hint + error IDs
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  // When disabled (no Smarty key), render a plain text input
  if (!enabled) {
    return (
      <div className={error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'}>
        <label className="usa-label" htmlFor={inputId}>
          {label}
          {isRequired && <span className="text-secondary-dark"> *</span>}
        </label>
        {hint && (
          <span className="usa-hint" id={hintId}>
            {hint}
          </span>
        )}
        {error && (
          <span className="usa-error-message" id={errorId} role="alert">
            {error}
          </span>
        )}
        <input
          id={inputId}
          className={`usa-input${error ? ' usa-input--error' : ''}`}
          name={name}
          type="text"
          value={value}
          onChange={onChange}
          aria-required={isRequired || undefined}
          aria-invalid={!!error || undefined}
          aria-describedby={describedBy}
          {...inputProps}
        />
      </div>
    )
  }

  const activeOptionId = activeIndex >= 0 ? `${baseId}-option-${activeIndex}` : undefined

  return (
    <div className={`${error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'} ${styles.wrapper}`}>
      <label className="usa-label" htmlFor={inputId}>
        {label}
        {isRequired && <span className="text-secondary-dark"> *</span>}
      </label>
      {hint && (
        <span className="usa-hint" id={hintId}>
          {hint}
        </span>
      )}
      {error && (
        <span className="usa-error-message" id={errorId} role="alert">
          {error}
        </span>
      )}
      <input
        ref={inputRef}
        id={inputId}
        className={`usa-input${error ? ' usa-input--error' : ''}`}
        name={name}
        type="text"
        role="combobox"
        value={value}
        onChange={onChange}
        onKeyDown={handleKeyDown}
        onFocus={open}
        onBlur={() => {
          // Delay dismiss so click on option registers first
          setTimeout(() => dismiss(), 200)
        }}
        aria-expanded={isOpen}
        aria-autocomplete="list"
        aria-controls={isOpen ? listboxId : undefined}
        aria-activedescendant={activeOptionId}
        aria-required={isRequired || undefined}
        aria-invalid={!!error || undefined}
        aria-describedby={describedBy}
        autoComplete="off"
        {...inputProps}
      />
      {isOpen && (
        <ul id={listboxId} role="listbox" className={styles.listbox}>
          {suggestions.map((suggestion, index) => {
            const optionId = `${baseId}-option-${index}`
            const isFocused = index === activeIndex
            let display = suggestion.street_line
            if (suggestion.secondary) display += ` ${suggestion.secondary}`
            if (suggestion.entries > 1) display += ` (${suggestion.entries} more entries)`
            display += `, ${suggestion.city} ${suggestion.state} ${suggestion.zipcode}`

            return (
              <li
                key={optionId}
                id={optionId}
                role="option"
                className={styles.option}
                data-focused={isFocused || undefined}
                aria-selected={isFocused}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handleSuggestionClick(index)}
              >
                {display}
              </li>
            )
          })}
        </ul>
      )}
      <div
        id={statusId}
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {isOpen && suggestions.length > 0
          ? `${suggestions.length} suggestion${suggestions.length !== 1 ? 's' : ''} available`
          : ''}
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Create `index.ts` barrel export**

```typescript
export { AddressAutocomplete } from './AddressAutocomplete'
export type { SelectedAddress } from './types'
```

- [ ] **Step 6: Run tests to verify they pass**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressAutocomplete/AddressAutocomplete.test.tsx
```

Expected: all tests PASS

- [ ] **Step 7: Commit**

```bash
git add src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/
git commit -m "feat: add AddressAutocomplete combobox component with ARIA and keyboard navigation"
```

---

### Task 5: AddressForm Integration

Replace the plain `InputField` for street address line 1 with `AddressAutocomplete` and wire up form field population on suggestion selection.

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.tsx`
- Modify: `src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.test.tsx`

- [ ] **Step 1: Write the integration tests for autocomplete in AddressForm**

Add these tests to the existing `AddressForm.test.tsx`, at the end of the `describe('AddressForm', ...)` block:

```typescript
  // --- Autocomplete integration ---

  describe('with Smarty autocomplete enabled', () => {
    beforeEach(() => {
      process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY = 'test-embedded-key'
    })

    afterEach(() => {
      delete process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
    })

    it('renders street address as a combobox when Smarty key is configured', () => {
      renderForm()
      expect(
        screen.getByRole('combobox', { name: /street address/i })
      ).toBeInTheDocument()
    })

    it('populates all form fields when an autocomplete suggestion is selected', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })
      server.use(
        http.get('https://us-autocomplete-pro.api.smarty.com/lookup', () =>
          HttpResponse.json({
            suggestions: [
              {
                street_line: '1600 Pennsylvania Ave NW',
                secondary: '',
                city: 'Washington',
                state: 'DC',
                zipcode: '20500',
                entries: 0
              }
            ]
          })
        )
      )

      const { user } = renderForm()
      const input = screen.getByRole('combobox', { name: /street address/i })

      await user.type(input, '1600 Penn')
      await vi.advanceTimersByTimeAsync(300)

      await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())
      await user.click(screen.getByRole('option'))

      expect(getStreetInput()).toHaveValue('1600 Pennsylvania Ave NW')
      expect(getCityInput()).toHaveValue('Washington')
      expect(getStateSelect()).toHaveValue('DC')
      expect(getPostalInput()).toHaveValue('20500')

      vi.useRealTimers()
    })
  })

  it('renders street address as a plain textbox when Smarty key is not configured', () => {
    delete process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY
    renderForm()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(
      screen.getByRole('textbox', { name: /^street address(?! line)/i })
    ).toBeInTheDocument()
  })
```

Note: you will also need to add `vi` and `afterEach` to the import from `vitest` if not already present, and add `waitFor` to the `@testing-library/react` import. You may need to update the existing `getStreetInput()` helper to also check for the `combobox` role:

```typescript
function getStreetInput() {
  return (
    screen.queryByRole('combobox', { name: /^street address(?! line)/i }) ??
    screen.getByRole('textbox', { name: /^street address(?! line)/i })
  )
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressForm/AddressForm.test.tsx
```

Expected: the new autocomplete tests FAIL (AddressAutocomplete not yet used in AddressForm). Existing tests should still PASS.

- [ ] **Step 3: Modify `AddressForm.tsx` to use `AddressAutocomplete`**

In `src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.tsx`:

Add import at top:

```typescript
import { AddressAutocomplete, type SelectedAddress } from '../AddressAutocomplete'
```

Replace the street address line 1 `InputField` (the first `<InputField>` with `name="streetAddress1"`) with:

```tsx
        <AddressAutocomplete
          label={t('labelStreetAddress', 'Street address')}
          {...(currentState === 'dc'
            ? { hint: t('hintStreetAddressDc', 'Include direction. NW, NE, SE, or SW.') }
            : {})}
          name="streetAddress1"
          value={streetAddress1}
          onChange={(e) => setStreetAddress1(e.target.value)}
          onSuggestionSelected={(address: SelectedAddress) => {
            setStreetAddress1(address.streetLine1)
            setStreetAddress2(address.streetLine2)
            setCity(address.city)
            setStateValue(address.state)
            setPostalCode(address.zipcode)
          }}
          autoComplete="address-line1"
          isRequired
          {...(fieldErrors.streetAddress1 ? { error: fieldErrors.streetAddress1 } : {})}
        />
```

- [ ] **Step 4: Run all AddressForm tests to verify they pass**

Run:
```bash
cd src/SEBT.Portal.Web && npx vitest run src/features/address/components/AddressForm/AddressForm.test.tsx
```

Expected: ALL tests PASS (both existing and new).

- [ ] **Step 5: Run the full frontend test suite**

Run:
```bash
cd src/SEBT.Portal.Web && pnpm test
```

Expected: all 560+ tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.tsx \
        src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.test.tsx
git commit -m "feat: integrate Smarty address autocomplete into AddressForm with graceful fallback"
```

---

## Verification Checklist

After all tasks are complete, verify:

- [ ] `cd src/SEBT.Portal.Web && pnpm test` — all frontend tests pass
- [ ] `cd src/SEBT.Portal.Web && pnpm lint` — no lint errors
- [ ] `cd src/SEBT.Portal.Web && pnpm knip` — no dead code detected
- [ ] With `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY` set: typing in street address field shows suggestions
- [ ] Without `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY`: form works identically to before (plain input)
- [ ] Keyboard navigation: ArrowDown/ArrowUp moves through suggestions, Enter selects, Escape dismisses
- [ ] Screen reader: live region announces suggestion count
- [ ] Multi-unit building: selecting a multi-entry suggestion expands to show unit options
- [ ] Selecting a suggestion populates street 1, street 2, city, state, and ZIP
