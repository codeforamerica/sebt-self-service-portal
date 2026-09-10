import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'

import { FeatureFlagsContext } from '@/features/feature-flags'

import { BenefitExpirationBanner } from './BenefitExpirationBanner'

function withApplyFlag(children: ReactNode, enableApply: boolean) {
  return (
    <FeatureFlagsContext.Provider
      value={{ flags: { enable_apply: enableApply }, isLoading: false, isError: false }}
    >
      {children}
    </FeatureFlagsContext.Provider>
  )
}

describe('BenefitExpirationBanner', () => {
  it('renders the expiration warning when applications are closed', () => {
    render(withApplyFlag(<BenefitExpirationBanner />, false))

    const alert = screen.getByRole('alert')
    expect(alert).toBeInTheDocument()
    // Test bundle is DC content.
    expect(alert).toHaveTextContent(
      'Summer 2026 DC SUN Bucks were issued beginning in June 2026 and expire 122 days after issuance'
    )
    expect(alert).toHaveTextContent(
      'Check each enrolled student here to see when their benefits expire.'
    )
  })

  it('renders nothing while applications are open', () => {
    const { container } = render(withApplyFlag(<BenefitExpirationBanner />, true))

    expect(container).toBeEmptyDOMElement()
  })

  it('renders the warning outside the feature-flags provider (fail closed)', () => {
    render(<BenefitExpirationBanner />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
  })
})
