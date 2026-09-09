'use client'

import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { setReplacementFlash } from '@/features/cards/utils/replacementFlash'
import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { trackCardReplacementSubmit } from '@/lib/analytics-helpers'
import { useDataLayer } from '@sebt/analytics'
import {
  Alert,
  Button,
  getState,
  ProcessingIndicator,
  RichText,
  SummaryBox
} from '@sebt/design-system'

import { useRequestCardReplacement } from '../../api/client'

interface ConfirmRequestProps {
  cases: SummerEbtCase[]
  address: Address
  onBack: () => void
}

// Copy templates use bracket placeholders (e.g. "[First name]", "[9999]") from
// the CSV-driven content pipeline. The case model has no child middle name, so
// "[M.]" is dropped along with its leading space. Returns null when a
// placeholder can't be filled (e.g. a card-number template without a known
// last 4), so callers can skip the line rather than show a raw placeholder.
function fillCardPlaceholders(template: string, ebtCase: SummerEbtCase): string | null {
  let filled = template
    .replace(/\s*\[M\.\]/, '')
    .replace('[First name]', ebtCase.childFirstName)
    .replace('[Last name]', ebtCase.childLastName)
  if (ebtCase.ebtCardLastFour) {
    filled = filled.replace('[9999]', ebtCase.ebtCardLastFour)
  }
  return filled.includes('[') ? null : filled
}

export function ConfirmRequest({ cases, address, onBack }: ConfirmRequestProps) {
  const { t } = useTranslation('result')
  const { t: tOptional } = useTranslation('optionalId')
  const { t: tCommon } = useTranslation('common')
  const { t: tDashboard } = useTranslation('dashboard')

  const router = useRouter()
  const currentState = getState()
  const mutation = useRequestCardReplacement()
  const { setPageData, trackEvent } = useDataLayer()
  // Store the i18n key (not the resolved string) so the banner re-translates at render
  // time when the user switches language (DC-454).
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const caseRefs = cases
    .filter((c): c is SummerEbtCase & { summerEBTCaseID: string } => c.summerEBTCaseID != null)
    .map((c) => ({
      summerEbtCaseId: c.summerEBTCaseID,
      applicationId: c.applicationId ?? null,
      applicationStudentId: c.applicationStudentId ?? null
    }))

  function handleSubmit() {
    setErrorKey(null)
    mutation.mutate(
      { caseRefs },
      {
        onSuccess: () => {
          trackCardReplacementSubmit({ setPageData, trackEvent }, null)
          // Hand the replaced cards to the dashboard banner in memory — names
          // and card digits are PII and must not ride the URL.
          setReplacementFlash(
            cases.map((c) => ({
              childFirstName: c.childFirstName,
              childLastName: c.childLastName,
              ebtCardLastFour: c.ebtCardLastFour ?? null
            }))
          )
          router.push('/dashboard?flash=card_replaced')
        },
        onError: (err) => {
          trackCardReplacementSubmit({ setPageData, trackEvent }, err)
          setErrorKey('alertCardReplaceError')
        }
      }
    )
  }

  // The single-card (S6) and bulk (S5) confirm screens share this layout but
  // differ in copy ("Order card" vs "Order cards", singular vs plural body and
  // address line). The bulk strings are generated into the optionalId
  // namespace, the single-card ones into result; both expose the same key
  // names. The H1 always comes from result — the bulk title is identical per
  // state and its generated key is shadowed by the card-selection screen's.
  const isMultiCardOrder = cases.length > 1
  const tOrder = isMultiCardOrder ? tOptional : t

  const singleCase = isMultiCardOrder ? undefined : cases[0]
  const preTitle = singleCase ? fillCardPlaceholders(t('pre-title'), singleCase) : null

  // tOrder('body') is \n-delimited list items — split and filter empties
  const replacingCards = tOrder('body').split('\n').filter(Boolean)

  return (
    <div>
      {preTitle && <p className="margin-bottom-0">{preTitle}</p>}
      <h1 className="font-sans-xl text-primary">{t('title')}</h1>

      <div className="margin-top-05">
        <ul className="usa-list margin-top-2">
          {replacingCards.map((item, index) => (
            <li key={index}>
              <RichText>{item}</RichText>
            </li>
          ))}
        </ul>
      </div>

      <SummaryBox className="margin-top-3">
        <h2 className="font-sans-md margin-0">{tOrder('summaryTitle')}</h2>

        <ul className="usa-list margin-0">
          {cases.map((c) => (
            <li key={c.summerEBTCaseID}>
              {fillCardPlaceholders(tOptional("who'sCard"), c)}
              {currentState === 'co' && c.ebtCardLastFour && (
                <span className="display-block">
                  {fillCardPlaceholders(tOptional('cardNumber'), c)}
                </span>
              )}
            </li>
          ))}
        </ul>

        <p className="margin-0 margin-top-1">{tOrder('summaryAddress')}</p>

        <address className="font-sans-sm">
          {address.streetAddress1 && (
            <span className="display-block">{address.streetAddress1}</span>
          )}
          {address.streetAddress2 && (
            <span className="display-block">{address.streetAddress2}</span>
          )}
          <span className="display-block">
            {address.city}, {address.state} {address.postalCode}
          </span>
        </address>
      </SummaryBox>

      {errorKey && (
        <Alert
          variant="error"
          className="margin-top-3"
        >
          {tDashboard(errorKey)}
        </Alert>
      )}

      {/* No inputs on this screen, so no fieldset; just the button-row half
          of the processing pattern. */}
      <div className="margin-top-3 display-flex flex-row flex-align-center gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={onBack}
          disabled={mutation.isPending}
        >
          {tCommon('back')}
        </Button>
        <Button
          type="button"
          onClick={handleSubmit}
          isLoading={mutation.isPending}
        >
          {tOrder('action')}
        </Button>
        <ProcessingIndicator
          isProcessing={mutation.isPending}
          label={tCommon('processing')}
        />
      </div>
    </div>
  )
}
