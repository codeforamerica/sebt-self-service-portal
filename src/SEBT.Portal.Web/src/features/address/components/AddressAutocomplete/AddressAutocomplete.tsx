'use client'

import {
  useId,
  useRef,
  useState,
  type ChangeEvent,
  type InputHTMLAttributes,
  type KeyboardEvent
} from 'react'

import { getState } from '@sebt/design-system'

import styles from './AddressAutocomplete.module.scss'
import type { SelectedAddress } from './types'
import { useAddressAutocomplete } from './useAddressAutocomplete'

interface AddressAutocompleteProps extends Omit<
  InputHTMLAttributes<HTMLInputElement>,
  'id' | 'role'
> {
  label: string
  name: string
  value: string
  onChange: (e: ChangeEvent<HTMLInputElement>) => void
  onSuggestionSelected: (address: SelectedAddress) => void
  error?: string
  hint?: string
  isRequired?: boolean
}

export function AddressAutocomplete({
  label,
  name,
  value,
  onChange,
  onSuggestionSelected,
  error,
  hint,
  isRequired,
  ...inputProps
}: AddressAutocompleteProps) {
  const baseId = useId()
  const inputId = `${baseId}-input`
  const listboxId = `${baseId}-listbox`
  const statusId = `${baseId}-status`
  const hintId = hint ? `${baseId}-hint` : undefined
  const errorId = error ? `${baseId}-error` : undefined

  const inputRef = useRef<HTMLInputElement>(null)
  const [activeIndex, setActiveIndex] = useState(-1)

  const smartyKey = process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY ?? ''
  const enabled = smartyKey.length > 0

  const autocomplete = useAddressAutocomplete({
    search: value,
    stateCode: getState(),
    onSelect: (address) => {
      onSuggestionSelected(address)
      setActiveIndex(-1)
    }
  })

  const { suggestions, isOpen, selectSuggestion, dismiss, open } = autocomplete

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!isOpen) {
      if (e.key === 'ArrowDown') {
        open()
        e.preventDefault()
      }
      return
    }

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        setActiveIndex((prev) => Math.min(prev + 1, suggestions.length - 1))
        break
      case 'ArrowUp':
        e.preventDefault()
        setActiveIndex((prev) => Math.max(prev - 1, -1))
        break
      case 'Enter':
        if (activeIndex >= 0) {
          e.preventDefault()
          selectSuggestion(activeIndex)
          setActiveIndex(-1)
        }
        break
      case 'Escape':
        e.preventDefault()
        dismiss()
        setActiveIndex(-1)
        break
    }
  }

  function handleSuggestionClick(index: number) {
    selectSuggestion(index)
    setActiveIndex(-1)
    inputRef.current?.focus()
  }

  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  // When disabled (no Smarty key), render a plain text input
  if (!enabled) {
    return (
      <div className={error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'}>
        <label
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
          id={inputId}
          className={`usa-input${error ? ' usa-input--error' : ''}`}
          name={name}
          type="text"
          value={value}
          onChange={onChange}
          aria-required={isRequired || undefined}
          aria-invalid={!!error || undefined}
          aria-describedby={describedBy}
          {...inputProps}
        />
      </div>
    )
  }

  const activeOptionId = activeIndex >= 0 ? `${baseId}-option-${activeIndex}` : undefined

  return (
    <div
      className={`${error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'} ${styles.wrapper}`}
    >
      <label
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
        ref={inputRef}
        id={inputId}
        className={`usa-input${error ? ' usa-input--error' : ''}`}
        name={name}
        type="text"
        role="combobox"
        value={value}
        onChange={onChange}
        onKeyDown={handleKeyDown}
        onFocus={open}
        onBlur={() => {
          setTimeout(() => dismiss(), 200)
        }}
        aria-expanded={isOpen}
        aria-autocomplete="list"
        aria-controls={isOpen ? listboxId : undefined}
        aria-activedescendant={activeOptionId}
        aria-required={isRequired || undefined}
        aria-invalid={!!error || undefined}
        aria-describedby={describedBy}
        autoComplete="off"
        {...inputProps}
      />
      {isOpen && suggestions.length > 0 && (
        <ul
          id={listboxId}
          role="listbox"
          className={styles.listbox}
        >
          {suggestions.map((suggestion, index) => {
            const optionId = `${baseId}-option-${index}`
            const isFocused = index === activeIndex
            let display = suggestion.street_line
            if (suggestion.secondary) display += ` ${suggestion.secondary}`
            if (suggestion.entries > 1) display += ` (${suggestion.entries} more entries)`
            display += `, ${suggestion.city} ${suggestion.state} ${suggestion.zipcode}`

            return (
              <li
                key={optionId}
                id={optionId}
                role="option"
                className={styles.option}
                data-focused={isFocused || undefined}
                aria-selected={isFocused}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handleSuggestionClick(index)}
              >
                {display}
              </li>
            )
          })}
        </ul>
      )}
      <div
        id={statusId}
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {isOpen && suggestions.length > 0
          ? `${suggestions.length} suggestion${suggestions.length !== 1 ? 's' : ''} available`
          : ''}
      </div>
    </div>
  )
}
