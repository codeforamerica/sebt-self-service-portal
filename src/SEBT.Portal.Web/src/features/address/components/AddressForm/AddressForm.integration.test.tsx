/**
 * Integration flow test (D7): Validates the full address update navigation path.
 *
 * Tests: form → submit → mutation called → context populated → navigates to replacement cards.
 * The full end-to-end flow (dashboard CTA → form → prompt → dashboard alert) spans multiple
 * route-level components and would require a Playwright E2E test. This integration test
 * covers the form's submit-and-navigate behavior with real mutation + context wiring.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { AddressFlowProvider, useAddressFlow } from '../../context'
import { AddressForm } from './AddressForm'

const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: vi.fn()
  })
}))

vi.mock('@/lib/state', () => ({
  getState: () => 'dc'
}))

/** Test helper that reads context value after form submission. */
function ContextSpy() {
  const { address } = useAddressFlow()
  if (!address) return null
  return <div data-testid="context-spy">{JSON.stringify(address)}</div>
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

describe('AddressForm integration', () => {
  it('submits address, populates flow context, and navigates to replacement cards', async () => {
    const queryClient = createTestQueryClient()
    const user = userEvent.setup()

    render(
      <QueryClientProvider client={queryClient}>
        <AddressFlowProvider>
          <AddressForm initialAddress={null} />
          <ContextSpy />
        </AddressFlowProvider>
      </QueryClientProvider>
    )

    // Fill form — DC defaults pre-fill city and state
    const streetInput = screen.getByRole('textbox', { name: /^street address(?! line)/i })
    const postalInput = screen.getByRole('textbox', { name: /zip code/i })

    await user.type(streetInput, '456 K St NW')
    await user.type(postalInput, '20001')

    // Submit
    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    // Verify navigation
    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })

    // Verify context was populated with submitted address
    await waitFor(() => {
      const spy = screen.getByTestId('context-spy')
      const contextData = JSON.parse(spy.textContent!)
      expect(contextData.streetAddress1).toBe('456 K St NW')
      expect(contextData.city).toBe('Washington')
      expect(contextData.state).toBe('District of Columbia')
      expect(contextData.postalCode).toBe('20001')
    })
  })
})
