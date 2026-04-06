'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { useHouseholdData, type Child } from '@/features/household'
import type { Application } from '@/features/household/api/schema'
import { Alert, Button, getState } from '@sebt/design-system'

interface ChildWithCard {
  key: string
  child: Child
  last4DigitsOfCard?: string | null | undefined
}

function flattenChildren(applications: Application[]): ChildWithCard[] {
  return applications.flatMap((app, appIndex) =>
    app.children.map((child, i) => ({
      key: `${app.applicationNumber ?? `app-${appIndex}`}-${i}`,
      child,
      last4DigitsOfCard: app.last4DigitsOfCard
    }))
  )
}

export function CardSelection() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const currentState = getState()
  const { data, isLoading, isError } = useHouseholdData()

  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const errorRef = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (error) {
      errorRef.current?.focus()
    }
  }, [error])

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data) {
    return (
      <Alert variant="error">
        {t('cardSelectionLoadError', 'Unable to load household members. Please try again later.')}
      </Alert>
    )
  }

  const children = flattenChildren(data.applications)

  if (children.length === 0) {
    return (
      <Alert variant="info">
        {t('cardSelectionNoChildren', 'No children found in your household.')}
      </Alert>
    )
  }

  function toggleChild(key: string) {
    setSelectedKeys((prev) => {
      const next = new Set(prev)
      if (next.has(key)) {
        next.delete(key)
      } else {
        next.add(key)
      }
      return next
    })
    setError(null)
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (selectedKeys.size === 0) {
      setError(t('cardSelectionRequired', 'Please select at least one card.'))
      return
    }

    // TODO (DC-153): Call card replacement API with selected children.
    // For now, redirect to dashboard with success params.
    router.push('/dashboard?addressUpdated=true&cardsRequested=true')
  }

  return (
    <form
      className="usa-form maxw-full"
      onSubmit={handleSubmit}
      noValidate
    >
      <p className="usa-hint">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>

      <fieldset
        className="usa-fieldset"
        aria-label={t('cardSelectionLabel', 'Select which cards you want to replace')}
        aria-describedby={error ? 'card-selection-error' : undefined}
      >
        <legend className="usa-legend">
          {t('cardSelectionLabel', 'Select which cards you want to replace')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        {error && (
          <span
            ref={errorRef}
            id="card-selection-error"
            className="usa-error-message"
            role="alert"
            tabIndex={-1}
          >
            {error}
          </span>
        )}

        {children.map(({ key, child, last4DigitsOfCard }) => (
          <div
            key={key}
            className="usa-checkbox"
          >
            <input
              className="usa-checkbox__input usa-checkbox__input--tile"
              type="checkbox"
              id={`card-${key}`}
              name="selectedCards"
              value={key}
              checked={selectedKeys.has(key)}
              onChange={() => toggleChild(key)}
            />
            <label
              className="usa-checkbox__label"
              htmlFor={`card-${key}`}
            >
              {/* TODO: Use t('childCardLabel', { firstName, lastName }) once key is available in CSV */}
              {child.firstName} {child.lastName}&apos;s card
              {currentState === 'co' && last4DigitsOfCard && (
                <span className="usa-checkbox__label-description">
                  {/* TODO: Use t('cardNumberLabel', { last4 }) once key is available in CSV */}
                  Card number: {last4DigitsOfCard} (last 4 digits)
                </span>
              )}
            </label>
          </div>
        ))}
      </fieldset>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back', 'Back')}
        </Button>
        <Button type="submit">{tCommon('continue', 'Continue')}</Button>
      </div>
    </form>
  )
}
