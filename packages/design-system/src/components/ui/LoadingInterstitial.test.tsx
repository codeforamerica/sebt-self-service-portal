import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { LoadingInterstitial } from './LoadingInterstitial'

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
    expect(status).toHaveAttribute('aria-busy', 'true')
  })

})
