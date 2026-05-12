'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { isWithinCooldownPeriod } from '@/features/cards/utils/cooldown'
import { useHouseholdData } from '@/features/household'
import type { SummerEbtCase } from '@/features/household/api/schema'
import { Alert, Button, getState } from '@sebt/design-system'

interface CaseGroup {
  caseId: string
  childFirstName: string
  childLastName: string
  ebtCardLastFour?: string | null | undefined
}

function buildCaseGroups(cases: SummerEbtCase[]): CaseGroup[] {
  return cases
    .filter(
      (c): c is SummerEbtCase & { summerEBTCaseID: string } =>
        c.summerEBTCaseID != null &&
        c.allowCardReplacement &&
        !isWithinCooldownPeriod(c.cardRequestedAt) &&
        c.issuanceType !== 'TanfEbtCard' &&
        c.issuanceType !== 'SnapEbtCard'
    )
    .map((c) => ({
      caseId: c.summerEBTCaseID,
      childFirstName: c.childFirstName,
      childLastName: c.childLastName,
      ebtCardLastFour: c.ebtCardLastFour
    }))
}

interface CardSelectionProps {
  /**
   * Path pushed on submit with `?cases=...` appended. Defaults to the
   * address-flow sibling route `select/confirm` (relative). Callers outside
   * that tree should pass an explicit path (absolute or relative).
   */
  confirmPath?: string
}

export function CardSelection({ confirmPath = 'select/confirm' }: CardSelectionProps = {}) {
  const { t } = useTranslation('confirmInfo')
  const { t: tOptional } = useTranslation('optionalId')
  const { t: tCommon } = useTranslation('common')

  const router = useRouter()
  const currentState = getState()
  const { data, isLoading, isError } = useHouseholdData()

  const [selectedCases, setSelectedCases] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const errorRef = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (error) {
      errorRef.current?.focus()
    }
  }, [error])

  if (isLoading) {
    return <p>{tCommon('loading')}</p>
  }

  if (isError || !data) {
    return (
      <Alert variant="error">
        {t('cardSelectionLoadError', 'Unable to load household members. Please try again later.')}
      </Alert>
    )
  }

  const groups = buildCaseGroups(data.summerEbtCases)

  if (groups.length === 0) {
    const hasCases = data.summerEbtCases.length > 0
    return (
      <>
        <Alert variant="info">
          {hasCases
            ? t(
                'cardSelectionAllInCooldown',
                'All cards were recently replaced. Please try again later.'
              )
            : t('cardSelectionNoChildren', 'No children found in your household.')}
        </Alert>
        <div className="margin-top-3">
          <Button
            variant="outline"
            type="button"
            onClick={() => router.back()}
          >
            {tCommon('back')}
          </Button>
        </div>
      </>
    )
  }

  function toggleCase(caseId: string) {
    setSelectedCases((prev) => {
      const next = new Set(prev)
      if (next.has(caseId)) {
        next.delete(caseId)
      } else {
        next.add(caseId)
      }
      return next
    })
    setError(null)
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (selectedCases.size === 0) {
      setError(tCommon('helperSelectAtLeastOne'))
      return
    }

    const cases = Array.from(selectedCases).join(',')
    const separator = confirmPath.includes('?') ? '&' : '?'
    router.push(`${confirmPath}${separator}cases=${encodeURIComponent(cases)}`)
  }

  return (
    <form
      className="usa-form maxw-full"
      onSubmit={handleSubmit}
      noValidate
    >
      <p className="usa-hint">{tCommon('requiredFields')}</p>

      <fieldset
        className="usa-fieldset"
        aria-label={tOptional('labelSelectCards')}
        aria-describedby={error ? 'card-selection-error' : undefined}
      >
        <legend className="usa-legend">
          {tOptional('labelSelectCards')}
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
          const isSelected = selectedCases.has(group.caseId)

          return (
            <div
              key={group.caseId}
              className="usa-checkbox"
            >
              <input
                className="usa-checkbox__input usa-checkbox__input--tile"
                type="checkbox"
                id={`card-${group.caseId}`}
                name="selectedCards"
                value={group.caseId}
                checked={isSelected}
                onChange={() => toggleCase(group.caseId)}
              />
              <label
                className="usa-checkbox__label"
                htmlFor={`card-${group.caseId}`}
              >
                {group.childFirstName} {group.childLastName}&apos;s card
                {currentState === 'co' && group.ebtCardLastFour && (
                  <span className="usa-checkbox__label-description">
                    {/* TODO update with {t('cardNumber')} */}
                    Card number: {group.ebtCardLastFour} (last 4 digits)
                  </span>
                )}
              </label>
            </div>
          )
        })}
      </fieldset>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back')}
        </Button>
        <Button type="submit">{tCommon('continue')}</Button>
      </div>
    </form>
  )
}
