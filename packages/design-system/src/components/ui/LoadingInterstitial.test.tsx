import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

vi.mock('next/navigation', () => ({
  usePathname: () => '/callback'
}))

import { LoadingInterstitial } from './LoadingInterstitial'
import { PageFocusProvider } from './PageFocusProvider'

describe('LoadingInterstitial', () => {
  it('renders the title and message', () => {
    render(
      <LoadingInterstitial
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Please wait...')
    expect(screen.getByText('Do not exit the page.')).toBeInTheDocument()
  })

  it('exposes a polite live region so screen readers announce the wait', () => {
    render(
      <LoadingInterstitial
        title="Please wait..."
        message="Checking your information."
      />
    )

    const status = screen.getByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
  })

  it('never sets aria-busy on the live region, which would defer the announcement', () => {
    render(
      <LoadingInterstitial
        title="Please wait..."
        message="Checking your information."
      />
    )

    expect(screen.getByRole('status')).not.toHaveAttribute('aria-busy')
  })

  it('renders its title as a page heading that shares one loading screen identity', () => {
    render(
      <PageFocusProvider>
        <LoadingInterstitial
          title="Please wait..."
          message="Checking your information."
        />
      </PageFocusProvider>
    )

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveAttribute('tabindex', '-1')
    expect(document.title).toMatch(/^Please wait\.\.\. \| /)
  })

  it('renders a spinner hidden from assistive tech (live region announces the wait)', () => {
    const { container } = render(
      <LoadingInterstitial
        title="Please wait..."
        message="Checking your information."
      />
    )

    const spinner = container.querySelector('.usa-spinner')
    expect(spinner).not.toBeNull()
    expect(spinner).toHaveAttribute('aria-hidden', 'true')
  })
})
