'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { isWithinCooldownPeriod } from '@/features/cards/utils/cooldown'
import { useHouseholdData, type Child } from '@/features/household'
import type { Application } from '@/features/household/api/schema'
import { Alert, Button, getState } from '@sebt/design-system'

interface ApplicationGroup {
  applicationNumber: string
  children: Child[]
  last4DigitsOfCard?: string | null | undefined
}

function buildApplicationGroups(applications: Application[]): ApplicationGroup[] {
  return applications
    .filter(
      (app): app is Application & { applicationNumber: string } =>
        app.applicationNumber != null && !isWithinCooldownPeriod(app.cardRequestedAt)
    )
    .map((app) => ({
      applicationNumber: app.applicationNumber,
      children: app.children,
      last4DigitsOfCard: app.last4DigitsOfCard
    }))
}

export function CardSelection() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const currentState = getState()
  const { data, isLoading, isError } = useHouseholdData()

  const [selectedApps, setSelectedApps] = useState<Set<string>>(new Set())
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

  const groups = buildApplicationGroups(data.applications)

  if (groups.length === 0) {
    const hasApplications = data.applications.length > 0
    return (
      <Alert variant="info">
        {hasApplications
          ? t(
              'cardSelectionAllInCooldown',
              'All cards were recently replaced. Please try again later.'
            )
          : t('cardSelectionNoChildren', 'No children found in your household.')}
      </Alert>
    )
  }

  function toggleApplication(appNumber: string) {
    setSelectedApps((prev) => {
      const next = new Set(prev)
      if (next.has(appNumber)) {
        next.delete(appNumber)
      } else {
        next.add(appNumber)
      }
      return next
    })
    setError(null)
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (selectedApps.size === 0) {
      setError(t('cardSelectionRequired', 'Please select at least one card.'))
      return
    }

    const apps = Array.from(selectedApps).join(',')
    router.push(`select/confirm?apps=${encodeURIComponent(apps)}`)
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

        {groups.map((group) => {
          const isSelected = selectedApps.has(group.applicationNumber)
          const isMultiChild = group.children.length > 1

          return group.children.map((child, childIndex) => {
            const isFirstChild = childIndex === 0
            const isSiblingOfSelected = isSelected && !isFirstChild

            return (
              <div
                key={`${group.applicationNumber}-${childIndex}`}
                className="usa-checkbox"
              >
                <input
                  className="usa-checkbox__input usa-checkbox__input--tile"
                  type="checkbox"
                  id={`card-${group.applicationNumber}-${childIndex}`}
                  name="selectedCards"
                  value={group.applicationNumber}
                  checked={isSelected}
                  disabled={isSiblingOfSelected}
                  onChange={() => toggleApplication(group.applicationNumber)}
                />
                <label
                  className="usa-checkbox__label"
                  htmlFor={`card-${group.applicationNumber}-${childIndex}`}
                >
                  {child.firstName} {child.lastName}&apos;s card
                  {currentState === 'co' && group.last4DigitsOfCard && (
                    <span className="usa-checkbox__label-description">
                      Card number: {group.last4DigitsOfCard} (last 4 digits)
                    </span>
                  )}
                  {isSiblingOfSelected && isMultiChild && (
                    <span className="usa-checkbox__label-description text-base-dark">
                      These children share a card
                    </span>
                  )}
                </label>
              </div>
            )
          })
        })}
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
