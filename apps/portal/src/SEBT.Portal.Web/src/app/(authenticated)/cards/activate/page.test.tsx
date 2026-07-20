import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import CardActivationPage from './page'

const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    back: mockBack
  })
}))

describe('CardActivationPage', () => {
  beforeEach(() => {
    mockBack.mockClear()
  })

  it('renders the "Activate a card" heading', () => {
    render(<CardActivationPage />)

    expect(screen.getByRole('heading', { name: /activate a card/i })).toBeInTheDocument()
  })

  it('renders the activation instructions from the dashboard namespace', () => {
    render(<CardActivationPage />)

    // Distinctive phrases from the DC `dashboard.body` copy.
    expect(screen.getByText(/set the PIN number to activate the card/i)).toBeInTheDocument()
    expect(screen.getByText(/do not enter your own birthday/i)).toBeInTheDocument()
  })

  it('renders the EBT Customer Service number as a tap-to-call link', () => {
    render(<CardActivationPage />)

    const callLink = screen.getByRole('link', { name: /tap to call ebt customer service/i })
    expect(callLink).toHaveAttribute('href', 'tel:+18883282656')
  })

  it('tags the call link as an external_only CTA for analytics', () => {
    render(<CardActivationPage />)

    const callLink = screen.getByRole('link', { name: /tap to call ebt customer service/i })
    expect(callLink).toHaveAttribute('data-analytics-cta', 'card_activation_phone_call')
    expect(callLink).toHaveAttribute('data-analytics-cta-destination-type', 'external_only')
  })

  it('exposes a Back button that calls router.back()', async () => {
    const user = (await import('@testing-library/user-event')).default.setup()
    render(<CardActivationPage />)

    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })
})
