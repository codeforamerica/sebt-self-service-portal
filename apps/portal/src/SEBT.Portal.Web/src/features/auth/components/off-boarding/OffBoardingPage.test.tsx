import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { OffBoardingPage } from './OffBoardingPage'

const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush
  })
}))

const TEST_CONTACT_LINK = 'https://example.com/contact'
const TEST_APPLY_LINK = 'https://example.com/apply'

describe('OffBoardingPage', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('renders the heading and body text', () => {
    render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

    expect(screen.getByRole('heading', { name: /we're sorry/i })).toBeInTheDocument()
    expect(screen.getByText(/you can go back to enter an id number/i)).toBeInTheDocument()
  })

  it('renders Back and Contact us buttons', () => {
    render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()

    // The primary "Contact us" action button (not the footer help link)
    const contactLinks = screen.getAllByRole('link', { name: /contact us/i })
    expect(contactLinks[0]).toHaveAttribute('href', TEST_CONTACT_LINK)
    expect(contactLinks[0]).toHaveClass('usa-button')
  })

  it('does not show "Apply now" when canApply is false', () => {
    sessionStorage.setItem('offboarding_canApply', 'false')

    render(
      <OffBoardingPage
        contactLink={TEST_CONTACT_LINK}
        applyLink={TEST_APPLY_LINK}
      />
    )

    expect(screen.queryByRole('link', { name: /apply now/i })).not.toBeInTheDocument()
  })

  it('shows "Apply now" section when canApply is true', () => {
    sessionStorage.setItem('offboarding_canApply', 'true')

    render(
      <OffBoardingPage
        contactLink={TEST_CONTACT_LINK}
        applyLink={TEST_APPLY_LINK}
      />
    )

    expect(screen.getByRole('link', { name: /apply now/i })).toHaveAttribute(
      'href',
      TEST_APPLY_LINK
    )
  })

  it('does not render "Apply now" link when canApply is true but applyLink is omitted', () => {
    sessionStorage.setItem('offboarding_canApply', 'true')

    render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

    // The apply body text still renders (canApply is true), but no link appears
    expect(screen.queryByRole('link', { name: /apply now/i })).not.toBeInTheDocument()
  })

  it('reads offboarding context from sessionStorage', () => {
    sessionStorage.setItem('offboarding_reason', 'docVerificationFailed')
    sessionStorage.setItem('offboarding_canApply', 'true')

    render(
      <OffBoardingPage
        contactLink={TEST_CONTACT_LINK}
        applyLink={TEST_APPLY_LINK}
      />
    )

    // The component renders. Generic copy is preserved for unknown reasons.
    expect(screen.getByRole('heading', { name: /we're sorry/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /apply now/i })).toBeInTheDocument()
  })

  describe('reason-specific copy', () => {
    it('renders distinct heading and body when reason is noIdProvided', () => {
      sessionStorage.setItem('offboarding_reason', 'noIdProvided')

      render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

      // Distinct heading for the noIdProvided case, not the generic "we're sorry".
      expect(
        screen.getByRole('heading', { name: /we need an ID to verify you/i })
      ).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: /we're sorry/i })).not.toBeInTheDocument()

      // Body references providing one of the listed IDs.
      expect(screen.getByText(/one of the listed IDs/i)).toBeInTheDocument()
    })

    it('hides the Apply now block for noIdProvided even when canApply is true', () => {
      sessionStorage.setItem('offboarding_reason', 'noIdProvided')
      sessionStorage.setItem('offboarding_canApply', 'true')

      render(
        <OffBoardingPage
          contactLink={TEST_CONTACT_LINK}
          applyLink={TEST_APPLY_LINK}
        />
      )

      // canApply is true, but for noIdProvided we intentionally don't surface
      // "Apply now". The user hasn't failed; they just haven't provided an ID.
      expect(screen.queryByRole('link', { name: /apply now/i })).not.toBeInTheDocument()
    })

    it('renders generic copy when reason is null', () => {
      // No sessionStorage key set; lazy initializer returns null.
      render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

      expect(screen.getByRole('heading', { name: /we're sorry/i })).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: /we need an ID/i })).not.toBeInTheDocument()
    })

    it('renders generic copy when reason is idProofingFailed', () => {
      sessionStorage.setItem('offboarding_reason', 'idProofingFailed')

      render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

      expect(screen.getByRole('heading', { name: /we're sorry/i })).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: /we need an ID/i })).not.toBeInTheDocument()
    })

    it('renders generic copy when reason is unknown', () => {
      sessionStorage.setItem('offboarding_reason', 'someFutureReason')

      render(<OffBoardingPage contactLink={TEST_CONTACT_LINK} />)

      expect(screen.getByRole('heading', { name: /we're sorry/i })).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: /we need an ID/i })).not.toBeInTheDocument()
    })
  })
})
