'use client'

import { useEffect, useState } from 'react'

import { Button } from '@/components/ui'

import { useVerificationStatus } from '../../api'

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
  const [timerExpired, setTimerExpired] = useState(false)

  const { data, error, refetch } = useVerificationStatus(challengeId)

  // Derive the "still checking" state from the timer and error status
  const showStillChecking = timerExpired || !!error

  // Show "still checking" message after threshold
  useEffect(() => {
    const timer = setTimeout(() => {
      setTimerExpired(true)
    }, STILL_CHECKING_THRESHOLD_MS)

    return () => clearTimeout(timer)
  }, [])

  // React to terminal status changes
  useEffect(() => {
    if (data?.status === 'verified') {
      onVerified()
    } else if (data?.status === 'rejected') {
      onRejected(data.offboardingReason)
    }
  }, [data?.status, data?.offboardingReason, onVerified, onRejected])

  return (
    <section aria-label="Verification status">
      {!showStillChecking ? (
        <div className="text-center padding-y-6">
          {/* TODO: Use t('docVerify.verifying') once key is available in dc.csv */}
          <p className="font-sans-lg text-bold margin-bottom-2">Verifying your document...</p>
          <p className="font-sans-sm text-base-dark">
            This may take a moment. Please don&apos;t close this page.
          </p>
          {/* USWDS loading indicator */}
          <div
            className="margin-top-3"
            aria-busy="true"
            aria-live="polite"
          >
            <span className="text-base-dark">Checking verification status</span>
          </div>
        </div>
      ) : (
        <div className="text-center padding-y-6">
          {/* TODO: Use t('docVerify.stillChecking') once key is available in dc.csv */}
          <p className="font-sans-lg text-bold margin-bottom-2">
            We&apos;re still checking your document
          </p>
          <p className="font-sans-sm text-base-dark margin-bottom-3">
            Verification is taking longer than expected. You can check the status or try again
            later.
          </p>
          <Button
            type="button"
            onClick={() => refetch()}
          >
            {/* TODO: Use t('docVerify.actionCheckStatus') once key is available in dc.csv */}
            Check status
          </Button>
        </div>
      )}
    </section>
  )
}
