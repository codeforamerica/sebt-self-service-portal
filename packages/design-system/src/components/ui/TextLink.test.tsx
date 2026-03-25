/**
 * TextLink Component Unit Tests
 *
 * Tests the TextLink component behavior including:
 * - Rendering links with correct href
 * - Children rendering
 * - USWDS styling classes
 * - Custom className support
 * - Link-specific attributes (target, rel, etc.)
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { TextLink } from './TextLink'

describe('TextLink', () => {
  describe('Rendering', () => {
    it('should render with children', () => {
      render(<TextLink href="/test">Click me</TextLink>)

      expect(screen.getByRole('link', { name: /click me/i })).toBeInTheDocument()
    })

    it('should render with correct href', () => {
      render(<TextLink href="/destination">Go to destination</TextLink>)

      expect(screen.getByRole('link')).toHaveAttribute('href', '/destination')
    })

    it('should support external URLs', () => {
      render(<TextLink href="https://example.com">External link</TextLink>)

      expect(screen.getByRole('link')).toHaveAttribute('href', 'https://example.com')
    })
  })

  describe('Styling', () => {
    it('should apply USWDS text link classes by default', () => {
      render(<TextLink href="/test">Link text</TextLink>)

      const link = screen.getByRole('link')
      expect(link).toHaveClass('text-bold')
      expect(link).toHaveClass('text-ink')
      expect(link).toHaveClass('text-underline')
    })

    it('should merge custom className with base classes', () => {
      render(
        <TextLink
          href="/test"
          className="my-custom-class"
        >
          Link text
        </TextLink>
      )

      const link = screen.getByRole('link')
      expect(link).toHaveClass('text-bold')
      expect(link).toHaveClass('text-ink')
      expect(link).toHaveClass('text-underline')
      expect(link).toHaveClass('my-custom-class')
    })

    it('should handle empty className gracefully', () => {
      render(
        <TextLink
          href="/test"
          className=""
        >
          Link text
        </TextLink>
      )

      const link = screen.getByRole('link')
      expect(link).toHaveClass('text-bold')
      expect(link).toHaveClass('text-ink')
      expect(link).toHaveClass('text-underline')
    })
  })

  describe('Link Attributes', () => {
    it('should support target attribute', () => {
      render(
        <TextLink
          href="https://example.com"
          target="_blank"
        >
          Open in new tab
        </TextLink>
      )

      expect(screen.getByRole('link')).toHaveAttribute('target', '_blank')
    })

    it('should support rel attribute', () => {
      render(
        <TextLink
          href="https://example.com"
          target="_blank"
          rel="noopener noreferrer"
        >
          External link
        </TextLink>
      )

      const link = screen.getByRole('link')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    })
  })

  describe('Children', () => {
    it('should render text children', () => {
      render(<TextLink href="/test">Simple text</TextLink>)

      expect(screen.getByText('Simple text')).toBeInTheDocument()
    })

    it('should render ReactNode children', () => {
      render(
        <TextLink href="/test">
          Click <strong>here</strong>
        </TextLink>
      )

      expect(screen.getByText('here')).toBeInTheDocument()
      expect(screen.getByText('here').tagName).toBe('STRONG')
    })
  })
})
