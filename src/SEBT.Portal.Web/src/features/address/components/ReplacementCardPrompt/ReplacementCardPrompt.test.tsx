import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { ReplacementCardPrompt } from './ReplacementCardPrompt'

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

const TEST_ADDRESS = {
  streetAddress1: '123 Main St NW',
  streetAddress2: 'Apt 4B',
  city: 'Washington',
  state: 'District of Columbia',
  postalCode: '20001'
}

describe('ReplacementCardPrompt', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
  })

  // --- Address display ---

  it('displays the saved address', () => {
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    expect(screen.getByText(/123 Main St NW/)).toBeInTheDocument()
    expect(screen.getByText(/Apt 4B/)).toBeInTheDocument()
    expect(screen.getByText(/Washington/)).toBeInTheDocument()
    expect(screen.getByText(/20001/)).toBeInTheDocument()
  })

  // --- Informational content ---

  it('shows replacement card criteria', () => {
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    expect(screen.getByText(/haven.t received them/i)).toBeInTheDocument()
    expect(screen.getByText(/no longer have them/i)).toBeInTheDocument()
  })

  // --- State-specific content ---

  it('shows SNAP/TANF callout for DC', () => {
    mockState = 'dc'
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    expect(screen.getByText(/SNAP or TANF/i)).toBeInTheDocument()
  })

  it('does not show SNAP/TANF callout for CO', () => {
    mockState = 'co'
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    expect(screen.queryByText(/SNAP or TANF/i)).not.toBeInTheDocument()
  })

  // --- Validation ---

  it('shows error when submitting without selection', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(screen.getByText(/select an option/i)).toBeInTheDocument()
  })

  it('focuses error message on validation failure', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const errorMessage = screen.getByText(/select an option/i)
    expect(errorMessage.closest('[tabindex="-1"]')).toHaveFocus()
  })

  it('links error message to fieldset via aria-describedby', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const fieldset = screen.getByRole('group', { name: /select one/i })
    expect(fieldset).toHaveAttribute('aria-describedby', expect.stringContaining('error'))
  })

  // --- Navigation ---

  it('navigates to dashboard with addressUpdated param when No is selected', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const noRadio = screen.getByRole('radio', { name: /no/i })
    await user.click(noRadio)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(mockPush).toHaveBeenCalledWith('/dashboard?addressUpdated=true')
  })

  it('navigates to card selection when Yes is selected', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const yesRadio = screen.getByRole('radio', { name: /yes/i })
    await user.click(yesRadio)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards/select')
  })

  it('navigates back when back button is clicked', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockBack).toHaveBeenCalled()
  })

  // --- Accessibility ---

  it('uses fieldset and legend for radio group', () => {
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const fieldset = screen.getByRole('group', { name: /select one/i })
    expect(fieldset).toBeInTheDocument()
  })

  it('allows keyboard navigation through radio options', async () => {
    const user = userEvent.setup()
    render(<ReplacementCardPrompt address={TEST_ADDRESS} />)

    const yesRadio = screen.getByRole('radio', { name: /yes/i })
    await user.click(yesRadio)
    expect(yesRadio).toBeChecked()

    const noRadio = screen.getByRole('radio', { name: /no/i })
    await user.click(noRadio)
    expect(noRadio).toBeChecked()
    expect(yesRadio).not.toBeChecked()
  })
})
