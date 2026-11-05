import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import enDcDev from '@/content/locales/en/dc/dev.json'
import AuthenticatedError from './error'

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard'
}))

function renderError(message: string) {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  render(
    <AuthenticatedError
      error={new globalThis.Error(message)}
      reset={() => {}}
    />
  )
}

// errorSomethingWentWrong has no row in the content sheet yet; the page ships its fallback copy.
const FALLBACK_HEADING = 'Something went wrong'

describe('Authenticated error boundary', () => {
  it('labels the section with a real level-1 heading for a generic error', () => {
    renderError('boom')

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveAttribute('id', 'error-heading')
    expect(heading).toHaveTextContent(FALLBACK_HEADING)
    expect(screen.getByRole('region', { name: FALLBACK_HEADING })).toBeInTheDocument()
  })

  it('uses the session copy as the heading for an authentication error', () => {
    renderError('Unauthorized')

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(enDcDev.alertSession)
  })
})
