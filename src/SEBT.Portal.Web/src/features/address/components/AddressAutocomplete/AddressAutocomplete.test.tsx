import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import type { SelectedAddress, SmartySuggestion } from './types'

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

// Lazy import so env var is set before module evaluation.
//
// Wraps AddressAutocomplete in a stateful container so that userEvent.type
// actually updates the `value` prop on each keystroke. Without this the
// component is stuck at value="" and the hook never fires a search.
async function renderAutocomplete(extraProps: Record<string, unknown> = {}) {
  const { AddressAutocomplete } = await import('./AddressAutocomplete')

  const onSuggestionSelected =
    (extraProps.onSuggestionSelected as (a: SelectedAddress) => void) ?? vi.fn()

  function Wrapper() {
    const [value, setValue] = useState('')
    return (
      <AddressAutocomplete
        label="Street address"
        name="streetAddress1"
        isRequired={true}
        {...extraProps}
        // value and onChange always use local state so typing works in tests
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onSuggestionSelected={onSuggestionSelected}
      />
    )
  }

  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
  const result = render(<Wrapper />)
  return { user, ...result, onSuggestionSelected }
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
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))

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
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [] })))

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
    expect(options[0]).toHaveClass('usa-combo-box__list-option--focused')
    expect(input).toHaveAttribute('aria-activedescendant', options[0]!.id)

    await user.keyboard('{ArrowDown}')
    expect(options[1]).toHaveClass('usa-combo-box__list-option--focused')
  })

  it('selects the focused suggestion on Enter', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))

    const onSuggestionSelected = vi.fn()
    const { user } = await renderAutocomplete({ onSuggestionSelected })

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)

    await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())

    await user.keyboard('{ArrowDown}{Enter}')

    expect(onSuggestionSelected).toHaveBeenCalledTimes(1)
  })

  it('closes suggestions on Escape', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({ suggestions: [makeSuggestion()] })))

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
    const units = [makeSuggestion({ secondary: 'Apt 1' }), makeSuggestion({ secondary: 'Apt 2' })]

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
    await user.click(screen.getAllByRole('option')[1]!)

    expect(onSuggestionSelected).toHaveBeenCalledWith(
      expect.objectContaining({ streetLine2: 'Apt 2' })
    )
  })

  // --- Stale-fetch race when typing under MIN_CHARS ---

  it('does not reopen suggestions when an in-flight fetch resolves after the input drops below MIN_CHARS', async () => {
    // Race scenario: user types past MIN_CHARS, the debounce fires, a fetch
    // starts. While that fetch is still in flight, the user deletes back under
    // MIN_CHARS. The hook's < MIN_CHARS branch must abort the in-flight fetch;
    // otherwise its late resolution writes stale suggestions and reopens the
    // listbox after the user has indicated they no longer want them.
    let fetchCount = 0
    let resolveFetch: (() => void) | null = null
    const fetchGate = new Promise<void>((resolve) => {
      resolveFetch = resolve
    })

    server.use(
      http.get(SMARTY_URL, async () => {
        fetchCount++
        await fetchGate
        return HttpResponse.json({
          suggestions: [makeSuggestion({ street_line: '123 Main St NW' })]
        })
      })
    )

    const { user } = await renderAutocomplete()
    const input = screen.getByRole('combobox')

    // Type past MIN_CHARS, fire debounce → fetch starts (gated open).
    await user.type(input, '123 M')
    await vi.advanceTimersByTimeAsync(300)
    expect(fetchCount).toBe(1)

    // Delete back below MIN_CHARS without letting the fetch resolve, then
    // fire the new debounce timer. The < MIN_CHARS branch should run and
    // abort the in-flight controller.
    await user.clear(input)
    await user.type(input, '12')
    await vi.advanceTimersByTimeAsync(300)

    // Now release the original fetch. With the bug present this would call
    // setSuggestions(...)/setIsOpen(true) and reopen the listbox.
    resolveFetch!()
    await vi.runAllTimersAsync()

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(screen.queryAllByRole('option')).toHaveLength(0)
  })

  // --- Selection should not re-trigger autocomplete ---

  it('does not re-fetch or re-open suggestions when selection rewrites the input value', async () => {
    // Smarty returns the canonical street_line ("123 Main Street NW"), which
    // typically differs from what the user typed ("123 Main"). When the parent
    // form propagates that canonical value back into the input, the hook must
    // not re-fetch and re-open the listbox — the user has already chosen.
    let fetchCount = 0
    server.use(
      http.get(SMARTY_URL, () => {
        fetchCount++
        return HttpResponse.json({
          suggestions: [makeSuggestion({ street_line: '123 Main Street NW' })]
        })
      })
    )

    const { AddressAutocomplete } = await import('./AddressAutocomplete')

    function Wrapper() {
      const [value, setValue] = useState('')
      return (
        <AddressAutocomplete
          label="Street address"
          name="streetAddress1"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onSuggestionSelected={(addr) => setValue(addr.streetLine1)}
          isRequired
        />
      )
    }

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Wrapper />)

    await user.type(screen.getByRole('combobox'), '123 Main')
    await vi.advanceTimersByTimeAsync(300)
    await waitFor(() => expect(screen.getByRole('option')).toBeInTheDocument())

    expect(fetchCount).toBe(1)
    await user.click(screen.getByRole('option'))

    // Drain the debounce window AND any in-flight fetch resolution.
    await vi.advanceTimersByTimeAsync(1000)
    await vi.runAllTimersAsync()

    // The selection-caused value change must not trigger another Smarty call,
    // and the listbox must remain closed.
    expect(fetchCount).toBe(1)
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })
})
