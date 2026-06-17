'use client'

import { RichText } from '@sebt/design-system'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'

import { interpolateDate } from '../../api'

interface CardStatusTimelineProps {
  cardRequestedAt: string | null | undefined
}

/**
 * Renders a single-state notice that a card replacement is in flight.
 * Shown only while the user is within the cooldown window after submitting
 * a replacement request — the gating decision lives in ChildCard so this
 * component does not need to know about cooldown duration.
 */
export function CardStatusTimeline({ cardRequestedAt }: CardStatusTimelineProps) {
  const { t, i18n } = useTranslation('dashboard')

  const rawLabel = t('cardTableStatusRequested', { defaultValue: '' })
  const label = interpolateDate(rawLabel, cardRequestedAt ?? null, i18n.language)
  const message = t('cardTableStatusMessageRequested2', { defaultValue: '' })

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <div className="display-flex flex-align-center padding-1 border-left-1 border-info bg-info-lighter">
          <Image
            src="/icons/credit_card_clock.svg"
            width={21}
            height={19}
            className="usa-icon margin-right-1 flex-shrink-0"
            alt=""
            aria-hidden="true"
          />
          <span>{label}</span>
        </div>
        <p className="margin-top-2 margin-bottom-0 text-base-dark font-body-xs">
          <RichText>{message}</RichText>
        </p>
      </dd>
    </div>
  )
}
