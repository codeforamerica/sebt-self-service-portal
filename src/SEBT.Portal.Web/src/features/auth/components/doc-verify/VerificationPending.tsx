'use client'

import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@sebt/design-system'

import { ApiError } from '@/api'
import { useVerificationStatus } from '../../api'
import { SK_STILL_CHECKING } from './sessionKeys'

// After this threshold, show the "still checking" message with a manual check button
const STILL_CHECKING_THRESHOLD_MS = 15000

interface VerificationPendingProps {
  challengeId: string
  onVerified: () => void
  onRejected: (offboardingReason?: string) => void
}

export function VerificationPending({
  challengeId,
  onVerified,
  onRejected
}: VerificationPendingProps) {
  const { t } = useTranslation('idProofing')
  // Persist "still checking" state across remounts so the UI doesn't oscillate
  const [timerExpired, setTimerExpired] = useState(
    () => sessionStorage.getItem(SK_STILL_CHECKING) === 'true'
  )

  const { data, error, refetch } = useVerificationStatus(challengeId)

  // Derive the "still checking" state from the timer and error status
  const showStillChecking = timerExpired || !!error

  // Show "still checking" message after threshold
  useEffect(() => {
    if (timerExpired) return
    const timer = setTimeout(() => {
      sessionStorage.setItem(SK_STILL_CHECKING, 'true')
      setTimerExpired(true)
    }, STILL_CHECKING_THRESHOLD_MS)

    return () => clearTimeout(timer)
  }, [timerExpired])

  // React to terminal status changes
  useEffect(() => {
    if (data?.status === 'verified') {
      onVerified()
    } else if (data?.status === 'rejected') {
      onRejected(data.offboardingReason ?? undefined)
    }
  }, [data?.status, data?.offboardingReason, onVerified, onRejected])

  // Treat 404 as terminal — challenge doesn't exist for this user
  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) {
      onRejected('challengeNotFound')
    }
  }, [error, onRejected])

  return (
    <section aria-label={t('verificationPendingAriaLabel', 'Verification status')}>
      {!showStillChecking ? (
        <div className="text-center padding-y-6">
          <p className="font-sans-lg text-bold margin-bottom-2">
            {t('verificationPendingHeading', 'Verifying your document...')}
          </p>
          <p className="font-sans-sm text-base-dark">
            {t('verificationPendingBody', "This may take a moment. Please don't close this page.")}
          </p>
          {/* USWDS loading indicator */}
          <div
            className="margin-top-3"
            aria-busy="true"
            aria-live="polite"
          >
            <span className="text-base-dark">
              {t('verificationPendingStatusLabel', 'Checking verification status')}
            </span>
          </div>
        </div>
      ) : (
        <div className="text-center padding-y-6">
          <p className="font-sans-lg text-bold margin-bottom-2">
            {t('verificationPendingStillCheckingHeading', "We're still checking your document")}
          </p>
          <p className="font-sans-sm text-base-dark margin-bottom-3">
            {t(
              'verificationPendingStillCheckingBody',
              'Verification is taking longer than expected. You can check the status or try again later.'
            )}
          </p>
          <Button
            type="button"
            onClick={() => refetch()}
          >
            {t('verificationPendingActionCheckStatus', 'Check status')}
          </Button>
        </div>
      )}
    </section>
  )
}
