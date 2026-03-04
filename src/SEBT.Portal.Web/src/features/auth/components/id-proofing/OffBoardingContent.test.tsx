/**
 * OffBoardingContent Component Unit Tests
 *
 * Tests the offboarding page content including:
 * - Title and body text rendering
 * - "Back" link navigating to id-proofing form
 * - "Contact us" link to external support page
 * - Conditional "Apply now" section based on canApply prop
 * - Optional body3 paragraph rendering (DC has it, CO does not)
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { OffBoardingContent } from './OffBoardingContent'

const DEFAULT_PROPS = {
  title: 'We\u2019re sorry, we aren\u2019t able to show your DC SUN Bucks information',
  body: 'You can go back to enter an ID number, or contact us if you need more help.',
  backHref: '/login/id-proofing',
  contactHref: 'https://sunbucks.dc.gov/page/contact-us',
  contactLabel: 'Contact us',
  canApply: true,
  applyBody:
    'If you\u2019re not sure what to do, tap "Apply now" and enter your child\u2019s information.',
  applyLabel: 'Apply now'
}

describe('OffBoardingContent', () => {
  describe('Core content', () => {
    it('renders the title as a heading', () => {
      render(<OffBoardingContent {...DEFAULT_PROPS} />)

      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(DEFAULT_PROPS.title)
    })

    it('renders the body text', () => {
      render(<OffBoardingContent {...DEFAULT_PROPS} />)

      expect(screen.getByText(DEFAULT_PROPS.body)).toBeInTheDocument()
    })
  })

  describe('Navigation buttons', () => {
    it('renders a "Back" link pointing to the id-proofing form', () => {
      render(<OffBoardingContent {...DEFAULT_PROPS} />)

      const backLink = screen.getByRole('link', { name: /back/i })
      expect(backLink).toHaveAttribute('href', '/login/id-proofing')
    })

    it('renders a "Contact us" link pointing to the external support page', () => {
      render(<OffBoardingContent {...DEFAULT_PROPS} />)

      const contactLink = screen.getByRole('link', {
        name: new RegExp(`${DEFAULT_PROPS.contactLabel}`)
      })
      expect(contactLink).toHaveAttribute('href', DEFAULT_PROPS.contactHref)
    })

    it('opens http(s) contact links in a new tab with SR disclosure', () => {
      render(<OffBoardingContent {...DEFAULT_PROPS} />)

      const contactLink = screen.getByRole('link', {
        name: /contact us/i
      })
      expect(contactLink).toHaveAttribute('target', '_blank')
      expect(contactLink).toHaveAttribute('rel', 'noopener noreferrer')
      expect(contactLink).toHaveTextContent(/opens in a new tab/i)
    })

    it('does not set target="_blank" for mailto: links', () => {
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          contactHref="mailto:help@example.com"
        />
      )

      const contactLink = screen.getByRole('link', {
        name: new RegExp(`${DEFAULT_PROPS.contactLabel}`)
      })
      expect(contactLink).not.toHaveAttribute('target')
      expect(contactLink).not.toHaveTextContent(/opens in a new tab/i)
    })
  })

  describe('Apply section', () => {
    it('renders the apply section body text when canApply is true', () => {
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          canApply={true}
        />
      )

      expect(screen.getByText(DEFAULT_PROPS.applyBody)).toBeInTheDocument()
    })

    it('renders the apply link when both applyLabel and applyHref are provided', () => {
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          canApply={true}
          applyHref="https://example.com/apply"
        />
      )

      const applyLink = screen.getByRole('link', { name: DEFAULT_PROPS.applyLabel })
      expect(applyLink).toHaveAttribute('href', 'https://example.com/apply')
    })

    it('does not render the apply link when applyHref is not provided', () => {
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          canApply={true}
        />
      )

      expect(screen.queryByRole('link', { name: DEFAULT_PROPS.applyLabel })).not.toBeInTheDocument()
    })

    it('does not render the apply section when canApply is false', () => {
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          canApply={false}
        />
      )

      expect(screen.queryByText(DEFAULT_PROPS.applyBody)).not.toBeInTheDocument()
      expect(screen.queryByRole('link', { name: DEFAULT_PROPS.applyLabel })).not.toBeInTheDocument()
    })

    it('renders applySkipBody when provided and non-empty', () => {
      const skipBody = 'You can skip this step by going back and typing in your ID number instead.'
      render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          applySkipBody={skipBody}
        />
      )

      expect(screen.getByText(skipBody)).toBeInTheDocument()
    })

    it('does not render applySkipBody when it is an empty string', () => {
      const { container } = render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          applySkipBody=""
        />
      )

      // The apply section should still be present (canApply is true) but body3 should not render
      expect(screen.getByText(DEFAULT_PROPS.applyBody)).toBeInTheDocument()
      // Ensure no extra empty paragraphs in the apply section
      const applySection = container.querySelector('[data-testid="apply-section"]')
      expect(applySection).toBeInTheDocument()
      // Only applyBody paragraph should be inside (no applySkipBody)
      const paragraphs = applySection!.querySelectorAll('p')
      expect(paragraphs).toHaveLength(1)
    })

    it('does not render the apply section when canApply is true but all content props are empty', () => {
      const { container } = render(
        <OffBoardingContent
          {...DEFAULT_PROPS}
          canApply={true}
          applyBody={undefined}
          applySkipBody={undefined}
          applyLabel={undefined}
          applyHref={undefined}
        />
      )

      expect(container.querySelector('[data-testid="apply-section"]')).not.toBeInTheDocument()
    })

    it('does not render applySkipBody when it is undefined', () => {
      const { container } = render(<OffBoardingContent {...DEFAULT_PROPS} />)

      const applySection = container.querySelector('[data-testid="apply-section"]')
      const paragraphs = applySection!.querySelectorAll('p')
      expect(paragraphs).toHaveLength(1)
    })
  })
})
