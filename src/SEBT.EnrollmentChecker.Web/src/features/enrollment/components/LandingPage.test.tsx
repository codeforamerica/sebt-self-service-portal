import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { LandingPage } from './LandingPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

describe('LandingPage', () => {
  it('renders a heading and a primary action button', () => {
    render(<LandingPage />)
    // Heading should be present (translation key resolves in test env)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    // Primary action button uses the 'action' i18n key (e.g. "Apply now" for CO)
    expect(screen.getByRole('button', { name: /apply now/i })).toBeInTheDocument()
  })

  it('navigates to /disclaimer on primary action button click', async () => {
    render(<LandingPage />)
    await userEvent.click(screen.getByRole('button', { name: /apply now/i }))
    expect(mockPush).toHaveBeenCalledWith('/disclaimer')
  })
})
