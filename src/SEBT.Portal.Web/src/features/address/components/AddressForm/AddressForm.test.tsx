import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { Address } from '@/features/household/api'
import { server } from '@/mocks/server'

import { AddressFlowProvider } from '../../context'
import { AddressForm } from './AddressForm'

const mockPush = vi.fn()
const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: mockBack
  })
}))

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => mockState,
    getStateLinks: () => ({
      help: { faqs: '#', contactUs: 'https://test.example/contact' },
      footer: {
        publicNotifications: '#',
        accessibility: '#',
        privacyAndSecurity: '#',
        googleTranslateDisclaimer: '#',
        about: '#',
        termsAndConditions: '#'
      },
      external: { contactUsAssistance: '#' }
    })
  }
})

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

function renderForm(initialAddress: Address | null = null) {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <AddressFlowProvider>
          <AddressForm initialAddress={initialAddress} />
        </AddressFlowProvider>
      </QueryClientProvider>
    )
  }
}

/** Helpers to find address form fields by accessible name. */
function getStreetInput() {
  return (
    screen.queryByRole('combobox', { name: /^street address(?! line)/i }) ??
    screen.getByRole('textbox', { name: /^street address(?! line)/i })
  )
}
function getLine2Input() {
  return screen.getByRole('textbox', { name: /street address line 2/i })
}
function getCityInput() {
  return screen.getByRole('textbox', { name: /city/i })
}
function getStateSelect() {
  return screen.getByRole('combobox', { name: /state or territory/i })
}
function getPostalInput() {
  return screen.getByRole('textbox', { name: /zip code/i })
}

describe('AddressForm', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockState = 'dc'

    // Portal target for site-level alerts
    let siteAlerts = document.getElementById('site-alerts')
    if (!siteAlerts) {
      siteAlerts = document.createElement('div')
      siteAlerts.id = 'site-alerts'
      document.body.appendChild(siteAlerts)
    }
    siteAlerts.innerHTML = ''
  })

  // --- Field rendering ---

  it('renders all required fields', () => {
    renderForm()

    expect(getStreetInput()).toBeInTheDocument()
    expect(getCityInput()).toBeInTheDocument()
    expect(getStateSelect()).toBeInTheDocument()
    expect(getPostalInput()).toBeInTheDocument()
  })

  it('renders street address line 2 as optional', () => {
    renderForm()

    const line2 = getLine2Input()
    expect(line2).toBeInTheDocument()
    expect(line2).not.toHaveAttribute('aria-required', 'true')
  })

  // --- State-specific defaults ---

  it('shows quadrant hint for DC', () => {
    mockState = 'dc'
    renderForm()

    expect(screen.getByText(/include direction/i)).toBeInTheDocument()
  })

  it('does not show quadrant hint for CO', () => {
    mockState = 'co'
    renderForm()

    expect(screen.queryByText(/include direction/i)).not.toBeInTheDocument()
  })

  it('pre-fills city as Washington for DC', () => {
    mockState = 'dc'
    renderForm()

    expect(getCityInput()).toHaveValue('Washington')
  })

  it('leaves city empty for CO', () => {
    mockState = 'co'
    renderForm()

    expect(getCityInput()).toHaveValue('')
  })

  it('pre-fills state as DC for DC', () => {
    mockState = 'dc'
    renderForm()

    expect(getStateSelect()).toHaveValue('DC')
  })

  it('pre-fills state as CO for CO', () => {
    mockState = 'co'
    renderForm()

    expect(getStateSelect()).toHaveValue('CO')
  })

  // --- Pre-population from addressOnFile ---

  it('pre-populates all fields from initialAddress', () => {
    const address: Address = {
      streetAddress1: '456 K St NW',
      streetAddress2: 'Suite 200',
      city: 'Arlington',
      state: 'Virginia',
      postalCode: '22201'
    }
    renderForm(address)

    expect(getStreetInput()).toHaveValue('456 K St NW')
    expect(getLine2Input()).toHaveValue('Suite 200')
    expect(getCityInput()).toHaveValue('Arlington')
    expect(getStateSelect()).toHaveValue('VA')
    expect(getPostalInput()).toHaveValue('22201')
  })

  it('falls back to state defaults when initialAddress is null', () => {
    mockState = 'dc'
    renderForm(null)

    expect(getStreetInput()).toHaveValue('')
    expect(getCityInput()).toHaveValue('Washington')
    expect(getStateSelect()).toHaveValue('DC')
    expect(getPostalInput()).toHaveValue('')
  })

  it('resolves state abbreviation from backend to dropdown value', () => {
    mockState = 'dc'
    const address: Address = {
      streetAddress1: '123 Main St NW',
      city: 'Washington',
      state: 'DC',
      postalCode: '20001'
    }
    renderForm(address)

    expect(getStateSelect()).toHaveValue('DC')
  })

  it('resolves a non-home-state abbreviation to the correct code', () => {
    mockState = 'dc'
    const address: Address = {
      streetAddress1: '456 Charles St',
      city: 'Baltimore',
      state: 'MD',
      postalCode: '21201'
    }
    renderForm(address)

    // If this were fallback-only, it would show "DC" (DC portal default)
    expect(getStateSelect()).toHaveValue('MD')
  })

  it('uses state defaults for individual null fields in initialAddress', () => {
    mockState = 'dc'
    const address: Address = {
      streetAddress1: '789 H St NE',
      streetAddress2: null,
      city: null,
      state: null,
      postalCode: '20002'
    }
    renderForm(address)

    expect(getStreetInput()).toHaveValue('789 H St NE')
    expect(getLine2Input()).toHaveValue('')
    expect(getCityInput()).toHaveValue('Washington')
    expect(getStateSelect()).toHaveValue('DC')
    expect(getPostalInput()).toHaveValue('20002')
  })

  // --- Validation ---

  it('shows errors when required fields are empty', async () => {
    mockState = 'co'
    const { user } = renderForm()

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const errorMessages = screen.getAllByRole('alert')
    expect(errorMessages.length).toBeGreaterThanOrEqual(1)
  })

  it('shows inline error when street address exceeds 30 characters', async () => {
    const { user } = renderForm()

    await user.type(getStreetInput(), '1234567890 Northeast Pennsylvania Ave NW')
    await user.type(getPostalInput(), '20001')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const inlineError = document.querySelector('.usa-error-message')
    expect(inlineError).toBeInTheDocument()
    expect(inlineError).toHaveTextContent(/shorter than 30 characters/i)
  })

  it('shows page-level error alert with contact link when street address exceeds 30 characters', async () => {
    const { user } = renderForm()

    await user.type(getStreetInput(), '1234567890 Northeast Pennsylvania Ave NW')
    await user.type(getPostalInput(), '20001')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const siteAlerts = document.getElementById('site-alerts')!
    expect(siteAlerts.textContent).toContain('issue with the address')

    const contactLink = siteAlerts.querySelector('a.usa-link')
    expect(contactLink).toBeInTheDocument()
    expect(contactLink).toHaveTextContent(/contact us/i)
    expect(contactLink).toHaveAttribute('href', expect.stringContaining('contact'))
  })

  it('allows street address of exactly 30 characters', async () => {
    server.use(
      http.put('/api/household/address', () => {
        return HttpResponse.json({ status: 'valid' }, { status: 200 })
      })
    )

    const { user } = renderForm()

    // Exactly 30 characters
    await user.type(getStreetInput(), '123456789012345678901234567890')
    await user.type(getPostalInput(), '20001')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  it('shows ZIP format error for invalid postal code', async () => {
    const { user } = renderForm()

    await user.type(getStreetInput(), '123 Main St NW')
    await user.clear(getPostalInput())
    await user.type(getPostalInput(), 'ABCDE')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(screen.getByText(/valid 5- or 9-digit zip/i)).toBeInTheDocument()
  })

  it('focuses error summary on validation failure', async () => {
    mockState = 'co'
    const { user } = renderForm()

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    await waitFor(() => {
      const errorSummary = screen.getByText(/please correct the errors/i)
      expect(errorSummary.closest('[tabindex="-1"]')).toHaveFocus()
    })
  })

  // --- Successful submission ---

  it('calls API and navigates on successful submission', async () => {
    const { user } = renderForm()

    await user.type(getStreetInput(), '123 Main St NW')
    await user.type(getPostalInput(), '20001')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  // --- Failed submission ---

  it('shows error alert on API failure', async () => {
    server.use(
      http.put('/api/household/address', () => {
        return HttpResponse.json({ error: 'Bad request' }, { status: 400 })
      })
    )

    const { user } = renderForm()

    await user.type(getStreetInput(), '123 Main St NW')
    await user.type(getPostalInput(), '20001')

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument()
    })
  })

  // --- Back button ---

  it('navigates back when back button is clicked', async () => {
    const { user } = renderForm()

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockBack).toHaveBeenCalled()
  })

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
      expect(screen.getByRole('combobox', { name: /street address/i })).toBeInTheDocument()
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

      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      const queryClient = createTestQueryClient()
      render(
        <QueryClientProvider client={queryClient}>
          <AddressFlowProvider>
            <AddressForm initialAddress={null} />
          </AddressFlowProvider>
        </QueryClientProvider>
      )

      const input = screen.getByRole('combobox', { name: /street address/i })

      await user.type(input, '1600 Penn')
      await vi.advanceTimersByTimeAsync(300)

      await waitFor(() =>
        expect(screen.getByRole('option', { name: /pennsylvania ave/i })).toBeInTheDocument()
      )
      await user.click(screen.getByRole('option', { name: /pennsylvania ave/i }))

      // Verify all fields were populated from the suggestion
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
    expect(
      screen.queryByRole('combobox', { name: /^street address(?! line)/i })
    ).not.toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /^street address(?! line)/i })).toBeInTheDocument()
  })
})
