/**
 * Details Component Unit Tests
 *
 * Tests the Details component behavior including:
 * - Summary rendering and heading level
 * - Expand/collapse state and ARIA wiring
 * - Keyboard operation
 * - Unique ids across multiple instances
 */
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { Details } from './Details'

const SUMMARY = 'Get more help accessing my account'

describe('Details', () => {
  describe('Rendering', () => {
    it('should render the summary as the button label', () => {
      render(<Details summary={SUMMARY}>Panel body</Details>)

      expect(screen.getByRole('button', { name: SUMMARY })).toBeInTheDocument()
    })

    it('should wrap the button in a level 2 heading by default', () => {
      render(<Details summary={SUMMARY}>Panel body</Details>)

      expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(SUMMARY)
    })

    it('should honor an explicit heading level', () => {
      render(
          <Details summary={SUMMARY} headingLevel={3}>
            Panel body
          </Details>
      )

      expect(screen.getByRole('heading', { level: 3 })).toBeInTheDocument()
    })

    it('should render the optional leading icon', () => {
      render(
          <Details summary={SUMMARY} icon={<span data-testid="leading-icon" />}>
            Panel body
          </Details>
      )

      expect(screen.getByTestId('leading-icon')).toBeInTheDocument()
    })
  })

  describe('Expand and collapse', () => {
    it('should toggle the panel and aria-expanded when the button is clicked', async () => {
      const user = userEvent.setup()
      render(<Details summary={SUMMARY}>Panel body</Details>)

      const button = screen.getByRole('button', { name: SUMMARY })
      const panel = screen.getByText('Panel body')

      expect(button).toHaveAttribute('aria-expanded', 'false')
      expect(panel).not.toBeVisible()

      await user.click(button)

      expect(button).toHaveAttribute('aria-expanded', 'true')
      expect(panel).toBeVisible()

      await user.click(button)

      expect(button).toHaveAttribute('aria-expanded', 'false')
      expect(panel).not.toBeVisible()
    })

    it('should start expanded when defaultExpanded is set', () => {
      render(
        <Details summary={SUMMARY} defaultExpanded>
          Panel body
        </Details>
      )

      expect(screen.getByRole('button', { name: SUMMARY })).toHaveAttribute(
        'aria-expanded',
        'true'
      )
      expect(screen.getByText('Panel body')).toBeVisible()
    })
  })

  describe('Keyboard', () => {
    it('should reach the button by tabbing', async () => {
      const user = userEvent.setup()
      render(<Details summary={SUMMARY}>Panel body</Details>)

      await user.tab()

      expect(screen.getByRole('button', { name: SUMMARY })).toHaveFocus()
    })

    it.each([
      ['Enter', '{Enter}'],
      ['Space', ' ']
    ])('should toggle with %s', async (_name, key) => {
      const user = userEvent.setup()
      render(<Details summary={SUMMARY}>Panel body</Details>)

      const button = screen.getByRole('button', { name: SUMMARY })
      await user.tab()
      await user.keyboard(key)

      expect(button).toHaveAttribute('aria-expanded', 'true')
      expect(screen.getByText('Panel body')).toBeVisible()
    })

    it('should keep panel content out of the tab order while collapsed', async () => {
      const user = userEvent.setup()
      render(
        <>
          <Details summary={SUMMARY}>
            <a href="https://example.gov/">Panel link</a>
          </Details>
          <button type="button">After</button>
        </>
      )

      await user.tab()
      expect(screen.getByRole('button', { name: SUMMARY })).toHaveFocus()

      // The collapsed panel is `hidden`, so tabbing skips its link entirely.
      await user.tab()
      expect(screen.getByRole('button', { name: 'After' })).toHaveFocus()
    })
  })

  describe('Accessibility', () => {
    it('should point aria-controls at the panel id', () => {
      render(<Details summary={SUMMARY}>Panel body</Details>)

      const controls = screen
        .getByRole('button', { name: SUMMARY })
        .getAttribute('aria-controls')

      expect(controls).toBeTruthy()
      expect(screen.getByText('Panel body')).toHaveAttribute('id', controls)
    })

    it('should give each instance its own panel id', () => {
      render(
        <>
          <Details summary="First">One</Details>
          <Details summary="Second">Two</Details>
        </>
      )

      const [first, second] = screen
        .getAllByRole('button')
        .map((button) => button.getAttribute('aria-controls'))

      expect(first).not.toBe(second)
    })

    it('should hide the chevron from assistive technology', () => {
      const { container } = render(<Details summary={SUMMARY}>Panel body</Details>)

      const chevron = container.querySelector('.usa-details__chevron')

      expect(chevron).toHaveAttribute('aria-hidden', 'true')
    })
  })
})
