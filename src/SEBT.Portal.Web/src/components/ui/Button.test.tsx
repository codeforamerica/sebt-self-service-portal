/**
 * Button Component Unit Tests
 *
 * Tests the Button component behavior including:
 * - Variant styling
 * - Loading states
 * - Accessibility attributes
 * - Event handling
 */
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createRef } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { Button } from './Button'

describe('Button', () => {
  describe('Rendering', () => {
    it('should render with children', () => {
      render(<Button>Click me</Button>)

      expect(screen.getByRole('button', { name: /click me/i })).toBeInTheDocument()
    })

    it('should default to type="button"', () => {
      render(<Button>Click me</Button>)

      expect(screen.getByRole('button')).toHaveAttribute('type', 'button')
    })

    it('should accept type="submit"', () => {
      render(<Button type="submit">Submit</Button>)

      expect(screen.getByRole('button')).toHaveAttribute('type', 'submit')
    })

    it('should forward ref to button element', () => {
      const ref = createRef<HTMLButtonElement>()
      render(<Button ref={ref}>Click me</Button>)

      expect(ref.current).toBeInstanceOf(HTMLButtonElement)
    })
  })

  describe('Variants', () => {
    it('should apply primary variant by default', () => {
      render(<Button>Primary</Button>)

      expect(screen.getByRole('button')).toHaveClass('usa-button')
      expect(screen.getByRole('button')).not.toHaveClass('usa-button--secondary')
    })

    it('should apply secondary variant class', () => {
      render(<Button variant="secondary">Secondary</Button>)

      expect(screen.getByRole('button')).toHaveClass('usa-button--secondary')
    })

    it('should apply outline variant class', () => {
      render(<Button variant="outline">Outline</Button>)

      expect(screen.getByRole('button')).toHaveClass('usa-button--outline')
    })

    it('should apply unstyled variant class', () => {
      render(<Button variant="unstyled">Unstyled</Button>)

      expect(screen.getByRole('button')).toHaveClass('usa-button--unstyled')
    })
  })

  describe('Full Width', () => {
    it('should not be full width by default', () => {
      render(<Button>Normal</Button>)

      expect(screen.getByRole('button')).not.toHaveClass('usa-button--full-width')
    })

    it('should apply full width class when fullWidth is true', () => {
      render(<Button fullWidth>Full Width</Button>)

      expect(screen.getByRole('button')).toHaveClass('usa-button--full-width')
    })
  })

  describe('Loading State', () => {
    it('should not be loading by default', () => {
      render(<Button>Click me</Button>)

      expect(screen.getByRole('button')).not.toHaveAttribute('aria-busy', 'true')
      expect(screen.getByRole('button')).toHaveTextContent('Click me')
    })

    it('should show loading state when isLoading is true', () => {
      render(<Button isLoading>Click me</Button>)

      expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true')
      expect(screen.getByRole('button')).toBeDisabled()
    })

    it('should show children when isLoading without loadingText', () => {
      render(<Button isLoading>Click me</Button>)

      expect(screen.getByRole('button')).toHaveTextContent('Click me')
    })

    it('should show loadingText when provided and isLoading', () => {
      render(
        <Button
          isLoading
          loadingText="Submitting..."
        >
          Click me
        </Button>
      )

      expect(screen.getByRole('button')).toHaveTextContent('Submitting...')
    })
  })

  describe('Disabled State', () => {
    it('should not be disabled by default', () => {
      render(<Button>Click me</Button>)

      expect(screen.getByRole('button')).not.toBeDisabled()
    })

    it('should be disabled when disabled prop is true', () => {
      render(<Button disabled>Click me</Button>)

      expect(screen.getByRole('button')).toBeDisabled()
    })

    it('should be disabled when isLoading is true', () => {
      render(<Button isLoading>Click me</Button>)

      expect(screen.getByRole('button')).toBeDisabled()
    })
  })

  describe('Event Handling', () => {
    it('should call onClick when clicked', async () => {
      const user = userEvent.setup()
      const handleClick = vi.fn()
      render(<Button onClick={handleClick}>Click me</Button>)

      await user.click(screen.getByRole('button'))

      expect(handleClick).toHaveBeenCalledTimes(1)
    })

    it('should not call onClick when disabled', async () => {
      const user = userEvent.setup()
      const handleClick = vi.fn()
      render(
        <Button
          disabled
          onClick={handleClick}
        >
          Click me
        </Button>
      )

      await user.click(screen.getByRole('button'))

      expect(handleClick).not.toHaveBeenCalled()
    })

    it('should not call onClick when loading', async () => {
      const user = userEvent.setup()
      const handleClick = vi.fn()
      render(
        <Button
          isLoading
          onClick={handleClick}
        >
          Click me
        </Button>
      )

      await user.click(screen.getByRole('button'))

      expect(handleClick).not.toHaveBeenCalled()
    })
  })

  describe('Custom className', () => {
    it('should merge custom className with base classes', () => {
      render(<Button className="my-custom-class">Click me</Button>)

      const button = screen.getByRole('button')
      expect(button).toHaveClass('usa-button')
      expect(button).toHaveClass('my-custom-class')
    })
  })
})
