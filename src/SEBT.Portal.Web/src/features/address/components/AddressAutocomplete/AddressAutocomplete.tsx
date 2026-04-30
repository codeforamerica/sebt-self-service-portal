'use client'

import {
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type ChangeEvent,
  type InputHTMLAttributes,
  type KeyboardEvent
} from 'react'

import { useTranslation } from 'react-i18next'

import { InputField, getState } from '@sebt/design-system'

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
  const { t } = useTranslation('confirmInfo')

  const baseId = useId()
  const inputId = `${baseId}-input`
  const listboxId = `${baseId}-listbox`
  const statusId = `${baseId}-status`
  const hintId = hint ? `${baseId}-hint` : undefined
  const errorId = error ? `${baseId}-error` : undefined

  const inputRef = useRef<HTMLInputElement>(null)
  const [activeIndex, setActiveIndex] = useState(-1)
  // Only pass the real search value to the hook after the user has typed.
  // This prevents pre-populated form values from triggering autocomplete on load.
  const [hasUserTyped, setHasUserTyped] = useState(false)

  const smartyKey = process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY ?? ''
  const enabled = smartyKey.length > 0

  const autocomplete = useAddressAutocomplete({
    search: hasUserTyped ? value : '',
    stateCode: getState(),
    onSelect: (address) => {
      onSuggestionSelected(address)
      setActiveIndex(-1)
      // Treat the selection-caused value change like a fresh prepopulation:
      // without this, the canonical street_line that flows back into `value`
      // re-fires the debounced search and the listbox immediately reopens.
      setHasUserTyped(false)
    }
  })

  const { suggestions, suggestionsVersion, isOpen, isLoading, selectSuggestion, dismiss, open } =
    autocomplete

  // Reset keyboard focus when suggestions change (e.g. secondary lookup replaces primary results).
  // useLayoutEffect fires synchronously after DOM mutations so the reset is applied before
  // the browser can paint a stale focused state or keyboard events observe stale activeIndex.
  // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional: synchronous reset guards against stale activeIndex after suggestion replacement
  useLayoutEffect(() => setActiveIndex(-1), [suggestionsVersion])

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

  // When disabled (no Smarty key), defer to the shared InputField primitive
  // so the form behaves identically to non-autocomplete fields.
  if (!enabled) {
    return (
      <InputField
        label={label}
        name={name}
        value={value}
        onChange={onChange}
        {...(error !== undefined && { error })}
        {...(hint !== undefined && { hint })}
        {...(isRequired !== undefined && { isRequired })}
        {...inputProps}
      />
    )
  }

  const activeOptionId = activeIndex >= 0 ? `${baseId}-option-${activeIndex}` : undefined

  return (
    <div
      className={`${error ? 'usa-form-group usa-form-group--error' : 'usa-form-group'} position-relative`}
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
        onChange={(e) => {
          setHasUserTyped(true)
          onChange(e)
        }}
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
          className="usa-combo-box__list"
        >
          {suggestions.map((suggestion, index) => {
            const optionId = `${baseId}-option-${index}`
            const isFocused = index === activeIndex
            let display = suggestion.street_line
            if (suggestion.secondary) display += ` ${suggestion.secondary}`
            if (suggestion.entries > 1)
              display += ` ${t('autocompleteMultiUnit', '({{count}} more entries)', { count: suggestion.entries })}`
            display += `, ${suggestion.city} ${suggestion.state} ${suggestion.zipcode}`

            return (
              <li
                key={optionId}
                id={optionId}
                role="option"
                className={`usa-combo-box__list-option${isFocused ? ' usa-combo-box__list-option--focused' : ''}`}
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
        aria-busy={isLoading}
        className="usa-sr-only"
      >
        {isOpen && suggestions.length > 0
          ? t('autocompleteSuggestionsAvailable', '{{count}} suggestion available', {
              count: suggestions.length
            })
          : ''}
      </div>
    </div>
  )
}
