'use client'

import { forwardRef, useId } from 'react'

import type { InputFieldProps } from './types'

export const InputField = forwardRef<HTMLInputElement, InputFieldProps>(function InputField(
  { label, error, hint, isRequired = false, className = '', disabled, ...props },
  ref
) {
  const generatedId = useId()
  const inputId = `input-${generatedId}`
  const labelId = `label-${generatedId}`
  const errorId = error ? `error-${generatedId}` : undefined
  const hintId = hint ? `hint-${generatedId}` : undefined

  const describedBy = [errorId, hintId].filter(Boolean).join(' ') || undefined
  const inputClassName = `usa-input ${error ? 'usa-input--error' : ''} ${className}`.trim()

  return (
    <div
      className={error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'}
      role="group"
      aria-labelledby={labelId}
    >
      <label
        id={labelId}
        className="usa-label"
        htmlFor={inputId}
      >
        {label}
        {isRequired && <span className="text-secondary-dark"> *</span>}
      </label>

      {hint && (
        <span
          className="usa-hint"
          id={hintId}
        >
          {hint}
        </span>
      )}

      {error && (
        <span
          className="usa-error-message"
          id={errorId}
          role="alert"
        >
          {error}
        </span>
      )}

      <input
        ref={ref}
        id={inputId}
        className={inputClassName}
        disabled={disabled}
        aria-required={isRequired}
        aria-invalid={!!error}
        aria-describedby={describedBy}
        {...props}
      />
    </div>
  )
})
