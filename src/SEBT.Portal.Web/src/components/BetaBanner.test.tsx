import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import { BetaBanner } from './BetaBanner'

function renderWithFlags(
  overrides: Partial<Record<keyof typeof TEST_FEATURE_FLAGS, boolean>> = {}
) {
  const flags: FeatureFlagsContextValue = {
    flags: { ...TEST_FEATURE_FLAGS, ...overrides },
    isLoading: false,
    isError: false
  }

  return render(
    <FeatureFlagsContext.Provider value={flags}>
      <BetaBanner />
    </FeatureFlagsContext.Provider>
  )
}

describe('BetaBanner', () => {
  it('renders nothing when enable_beta_banner is false', () => {
    const { container } = renderWithFlags({ enable_beta_banner: false })

    expect(container).toBeEmptyDOMElement()
  })

  it('renders an info alert when enable_beta_banner is true', () => {
    renderWithFlags({ enable_beta_banner: true })

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveClass('usa-alert--warning', 'margin-top-0')
  })

  it('renders localized banner text', () => {
    renderWithFlags({ enable_beta_banner: true })

    // i18n key: betaBannerText (common namespace)
    // Falls back to the default English string until the key is in the spreadsheet
    expect(
      screen.getByText(
        'This site is currently in beta. Some features may be incomplete or missing.'
      )
    ).toBeInTheDocument()
  })
})
