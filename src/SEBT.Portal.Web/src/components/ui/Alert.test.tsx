/**
 * Alert Component Unit Tests
 *
 * Tests the Alert component behavior including:
 * - Variant styling
 * - Heading and content rendering
 * - Slim and no-icon modifiers
 * - Accessibility attributes
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { Alert } from './Alert'

describe('Alert', () => {
  describe('Rendering', () => {
    it('should render with children content', () => {
      render(<Alert>This is an alert message</Alert>)

      expect(screen.getByRole('alert')).toBeInTheDocument()
      expect(screen.getByText('This is an alert message')).toBeInTheDocument()
    })

    it('should render heading when provided', () => {
      render(<Alert heading="Important Notice">Alert content</Alert>)

      expect(screen.getByRole('heading', { level: 4 })).toHaveTextContent('Important Notice')
    })

    it('should not render heading when not provided', () => {
      render(<Alert>Alert content</Alert>)

      expect(screen.queryByRole('heading')).not.toBeInTheDocument()
    })
  })

  describe('Variants', () => {
    it('should apply info variant by default', () => {
      render(<Alert>Info alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--info')
    })

    it('should apply success variant class', () => {
      render(<Alert variant="success">Success alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--success')
    })

    it('should apply warning variant class', () => {
      render(<Alert variant="warning">Warning alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--warning')
    })

    it('should apply error variant class', () => {
      render(<Alert variant="error">Error alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--error')
    })

    it('should apply emergency variant class', () => {
      render(<Alert variant="emergency">Emergency alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--emergency')
    })
  })

  describe('Modifiers', () => {
    it('should not be slim by default', () => {
      render(<Alert>Normal alert</Alert>)

      expect(screen.getByRole('alert')).not.toHaveClass('usa-alert--slim')
    })

    it('should apply slim class when slim is true', () => {
      render(<Alert slim>Slim alert</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--slim')
    })

    it('should show icon by default', () => {
      render(<Alert>Alert with icon</Alert>)

      expect(screen.getByRole('alert')).not.toHaveClass('usa-alert--no-icon')
    })

    it('should apply no-icon class when noIcon is true', () => {
      render(<Alert noIcon>Alert without icon</Alert>)

      expect(screen.getByRole('alert')).toHaveClass('usa-alert--no-icon')
    })

    it('should support combining slim and noIcon', () => {
      render(
        <Alert
          slim
          noIcon
        >
          Slim no-icon alert
        </Alert>
      )

      const alert = screen.getByRole('alert')
      expect(alert).toHaveClass('usa-alert--slim')
      expect(alert).toHaveClass('usa-alert--no-icon')
    })
  })

  describe('Custom className', () => {
    it('should merge custom className with base classes', () => {
      render(<Alert className="my-custom-class">Alert content</Alert>)

      const alert = screen.getByRole('alert')
      expect(alert).toHaveClass('usa-alert')
      expect(alert).toHaveClass('usa-alert--info')
      expect(alert).toHaveClass('my-custom-class')
    })
  })

  describe('Accessibility', () => {
    it('should have role="alert" for screen reader announcement', () => {
      render(<Alert>Accessible alert</Alert>)

      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    it('should have proper USWDS structure', () => {
      render(
        <Alert
          variant="error"
          heading="Error"
        >
          Something went wrong
        </Alert>
      )

      const alert = screen.getByRole('alert')
      expect(alert.querySelector('.usa-alert__body')).toBeInTheDocument()
      expect(alert.querySelector('.usa-alert__heading')).toBeInTheDocument()
      expect(alert.querySelector('.usa-alert__text')).toBeInTheDocument()
    })
  })
})
