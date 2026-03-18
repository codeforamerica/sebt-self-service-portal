import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

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
vi.mock('@/lib/state', () => ({
  getState: () => mockState
}))

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

function renderForm() {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <AddressFlowProvider>
          <AddressForm />
        </AddressFlowProvider>
      </QueryClientProvider>
    )
  }
}

/** Helpers to find address form fields by accessible name. */
function getStreetInput() {
  return screen.getByRole('textbox', { name: /^street address(?! line)/i })
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

  it('pre-fills state as District of Columbia for DC', () => {
    mockState = 'dc'
    renderForm()

    expect(getStateSelect()).toHaveValue('District of Columbia')
  })

  it('pre-fills state as Colorado for CO', () => {
    mockState = 'co'
    renderForm()

    expect(getStateSelect()).toHaveValue('Colorado')
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
    server.use(
      http.put('/api/household/address', () => {
        return new HttpResponse(null, { status: 204 })
      })
    )

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
})
