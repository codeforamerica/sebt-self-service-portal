/**
 * InputField Component Unit Tests
 *
 * Tests the InputField component behavior including:
 * - Label and input association
 * - Error and hint text
 * - Required field indication
 * - Accessibility attributes
 */
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createRef } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { InputField } from './InputField'

describe('InputField', () => {
  describe('Rendering', () => {
    it('should render with label', () => {
      render(<InputField label="Email address" />)

      // Use getByRole to avoid "multiple elements found" due to aria-labelledby on wrapper
      expect(screen.getByRole('textbox', { name: /email address/i })).toBeInTheDocument()
    })

    it('should render input with correct type', () => {
      render(
        <InputField
          label="Email"
          type="email"
        />
      )

      expect(screen.getByRole('textbox')).toHaveAttribute('type', 'email')
    })

    it('should forward ref to input element', () => {
      const ref = createRef<HTMLInputElement>()
      render(
        <InputField
          ref={ref}
          label="Test input"
        />
      )

      expect(ref.current).toBeInstanceOf(HTMLInputElement)
    })

    it('should pass through additional props', () => {
      render(
        <InputField
          label="Email"
          name="email"
          placeholder="Enter email"
          autoComplete="email"
        />
      )

      const input = screen.getByRole('textbox')
      expect(input).toHaveAttribute('name', 'email')
      expect(input).toHaveAttribute('placeholder', 'Enter email')
      expect(input).toHaveAttribute('autoComplete', 'email')
    })
  })

  describe('Required Field', () => {
    it('should not show required indicator by default', () => {
      render(<InputField label="Optional field" />)

      expect(screen.queryByText('*')).not.toBeInTheDocument()
      expect(screen.getByRole('textbox')).toHaveAttribute('aria-required', 'false')
    })

    it('should show required indicator when isRequired is true', () => {
      render(
        <InputField
          label="Required field"
          isRequired
        />
      )

      expect(screen.getByText('*')).toBeInTheDocument()
      expect(screen.getByRole('textbox')).toHaveAttribute('aria-required', 'true')
    })
  })

  describe('Hint Text', () => {
    it('should not render hint by default', () => {
      render(<InputField label="Field" />)

      expect(screen.queryByText(/hint/i)).not.toBeInTheDocument()
    })

    it('should render hint when provided', () => {
      render(
        <InputField
          label="Password"
          hint="Must be at least 8 characters"
        />
      )

      expect(screen.getByText('Must be at least 8 characters')).toBeInTheDocument()
    })

    it('should associate hint with input via aria-describedby', () => {
      render(
        <InputField
          label="Password"
          hint="Password hint"
        />
      )

      const input = screen.getByRole('textbox')
      const hint = screen.getByText('Password hint')

      expect(input).toHaveAttribute('aria-describedby', expect.stringContaining(hint.id))
    })
  })

  describe('Error State', () => {
    it('should not show error by default', () => {
      render(<InputField label="Field" />)

      expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'false')
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    })

    it('should show error message when error is provided', () => {
      render(
        <InputField
          label="Email"
          error="Please enter a valid email"
        />
      )

      expect(screen.getByRole('alert')).toHaveTextContent('Please enter a valid email')
    })

    it('should mark input as invalid when error is present', () => {
      render(
        <InputField
          label="Email"
          error="Invalid email"
        />
      )

      expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'true')
    })

    it('should apply error classes to input', () => {
      render(
        <InputField
          label="Email"
          error="Error"
        />
      )

      expect(screen.getByRole('textbox')).toHaveClass('usa-input--error')
    })

    it('should apply error classes to form group', () => {
      render(
        <InputField
          label="Email"
          error="Error"
        />
      )

      expect(screen.getByRole('group')).toHaveClass('usa-form-group--error')
    })

    it('should associate error with input via aria-describedby', () => {
      render(
        <InputField
          label="Email"
          error="Error message"
        />
      )

      const input = screen.getByRole('textbox')
      const error = screen.getByRole('alert')

      expect(input).toHaveAttribute('aria-describedby', expect.stringContaining(error.id))
    })
  })

  describe('Disabled State', () => {
    it('should not be disabled by default', () => {
      render(<InputField label="Field" />)

      expect(screen.getByRole('textbox')).not.toBeDisabled()
    })

    it('should be disabled when disabled prop is true', () => {
      render(
        <InputField
          label="Field"
          disabled
        />
      )

      expect(screen.getByRole('textbox')).toBeDisabled()
    })
  })

  describe('Event Handling', () => {
    it('should call onChange when value changes', async () => {
      const user = userEvent.setup()
      const handleChange = vi.fn()
      render(
        <InputField
          label="Email"
          onChange={handleChange}
        />
      )

      await user.type(screen.getByRole('textbox'), 'test@example.com')

      expect(handleChange).toHaveBeenCalled()
    })

    it('should call onBlur when input loses focus', async () => {
      const user = userEvent.setup()
      const handleBlur = vi.fn()
      render(
        <InputField
          label="Email"
          onBlur={handleBlur}
        />
      )

      const input = screen.getByRole('textbox')
      await user.click(input)
      await user.tab()

      expect(handleBlur).toHaveBeenCalled()
    })
  })

  describe('Custom className', () => {
    it('should merge custom className with base classes', () => {
      render(
        <InputField
          label="Field"
          className="my-custom-class"
        />
      )

      const input = screen.getByRole('textbox')
      expect(input).toHaveClass('usa-input')
      expect(input).toHaveClass('my-custom-class')
    })
  })

  describe('Accessibility', () => {
    it('should have proper label association', () => {
      render(<InputField label="Email address" />)

      const input = screen.getByRole('textbox')
      const label = screen.getByText('Email address')

      expect(input).toHaveAttribute('id')
      expect(label).toHaveAttribute('for', input.id)
    })

    it('should use role="group" on container with aria-labelledby', () => {
      render(<InputField label="Email" />)

      const group = screen.getByRole('group')
      const label = screen.getByText('Email')

      expect(group).toHaveAttribute('aria-labelledby', label.id)
    })

    it('should include both hint and error in aria-describedby', () => {
      render(
        <InputField
          label="Password"
          hint="At least 8 characters"
          error="Password is required"
        />
      )

      const input = screen.getByRole('textbox')
      const describedBy = input.getAttribute('aria-describedby') || ''

      expect(describedBy).toContain('error')
      expect(describedBy).toContain('hint')
    })
  })
})
