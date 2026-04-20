'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert } from '@sebt/design-system'

import {
  clearChallengeContext,
  SK_CHALLENGE_ID,
  SK_SUB_STATE,
  SubState
} from '@/features/auth/components/doc-verify/sessionKeys'
import { useStartChallenge, useVerificationStatus } from '../../api'
import { createDocVAdapter, type DocVAdapter, type DocVAdapterConfig } from './adapters'
import { DocVerifyCapture } from './DocVerifyCapture'
import { DocVerifyInterstitial } from './DocVerifyInterstitial'
import { VerificationPending } from './VerificationPending'

interface DocVerifyPageProps {
  contactLink: string
  sdkKey: string
}

function readChallengeContext(searchParams: URLSearchParams): {
  challengeId: string | null
  subState: SubState | null
} {
  // URL query param is primary source for challengeId, sessionStorage is fallback
  const urlChallengeId = searchParams.get('challengeId')
  const challengeId = urlChallengeId || sessionStorage.getItem(SK_CHALLENGE_ID)

  const persisted = sessionStorage.getItem(SK_SUB_STATE)
  const persistedChallengeId = sessionStorage.getItem(SK_CHALLENGE_ID)

  // Only trust persisted subState when it belongs to the current challenge.
  // A mismatch means old state from a prior DocV attempt is lingering.
  const challengeMatches = urlChallengeId != null && persistedChallengeId === urlChallengeId
  const subState =
    challengeMatches && (persisted === 'capture' || persisted === 'pending') ? persisted : null

  return { challengeId, subState }
}

export function DocVerifyPage({ contactLink, sdkKey }: DocVerifyPageProps) {
  const router = useRouter()
  const searchParams = useSearchParams()
  const { t } = useTranslation('idProofing')
  const startChallenge = useStartChallenge()

  const [subState, setSubState] = useState<SubState>('interstitial')
  const [challengeId, setChallengeId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { setPageData, trackEvent } = useDataLayer()

  // Capture launch config — set by handleContinue, consumed by DocVerifyCapture on mount
  const [captureLaunchConfig, setCaptureLaunchConfig] = useState<Omit<
    DocVAdapterConfig,
    'containerId'
  > | null>(null)

  // Create adapter once — stable across renders
  /* eslint-disable react-hooks/refs -- Intentional: lazy-init pattern reads ref to avoid recreating the adapter on every render */
  const adapterRef = useRef<DocVAdapter | null>(null)
  if (adapterRef.current == null) {
    adapterRef.current = createDocVAdapter()
  }
  const adapter = adapterRef.current
  /* eslint-enable react-hooks/refs */

  // Read challenge context on mount — URL query param is primary, sessionStorage is fallback (D6, D9)
  useEffect(() => {
    const ctx = readChallengeContext(searchParams)

    if (!ctx.challengeId) {
      // No challenge context — redirect to id-proofing form
      router.replace('/login/id-proofing')
      return
    }

    // If persisted state belongs to a different challenge, clear it so it
    // cannot bleed into this flow (e.g., after StrictMode double-invoke).
    if (ctx.subState === null) {
      clearChallengeContext()
    }

    setChallengeId(ctx.challengeId)
    // Persist to sessionStorage for mobile tab recovery
    sessionStorage.setItem(SK_CHALLENGE_ID, ctx.challengeId)

    // If the user was in capture (e.g., mobile tab recovery), skip to pending (D6)
    if (ctx.subState === 'capture' || ctx.subState === 'pending') {
      adapter.reset()
      setSubState('pending')
      sessionStorage.setItem(SK_SUB_STATE, 'pending')
    }
  }, [router, adapter, searchParams])

  // Poll status during interstitial (for allowIdRetry) and capture (safety net
  // in case the SDK's onSuccess never fires after remote mobile capture).
  const statusQuery = useVerificationStatus(
    (subState === 'interstitial' || subState === 'capture') && challengeId ? challengeId : undefined
  )
  const allowIdRetry = statusQuery.data?.allowIdRetry ?? false

  // "Continue" click handler — JIT token fetch, then transition to capture sub-state.
  // The actual adapter.launch() happens inside DocVerifyCapture after its container mounts.
  const handleContinue = async () => {
    if (!challengeId) return
    setError(null)

    trackEvent(AnalyticsEvents.DOCV_START)

    try {
      const { docvTransactionToken } = await startChallenge.mutateAsync(challengeId)

      // Build config for the capture component
      setCaptureLaunchConfig({
        sdkKey,
        token: docvTransactionToken,
        onSuccess: () => {
          sessionStorage.setItem(SK_SUB_STATE, 'pending')
          setSubState('pending')
        },
        onError: () => {
          sessionStorage.setItem('offboarding_reason', 'docVerificationFailed')
          sessionStorage.setItem('offboarding_canApply', 'false')
          clearChallengeContext()
          router.push('/login/id-proofing/off-boarding')
        }
      })

      // Persist sub-state for mobile tab recovery (D6) and transition
      sessionStorage.setItem(SK_SUB_STATE, 'capture')
      setSubState('capture')
    } catch {
      setError(
        t(
          'docVerifyStartError',
          'Something went wrong starting document verification. Please try again.'
        )
      )
    }
  }

  const handleEnterIdNumber = useCallback(() => {
    clearChallengeContext()
    router.push('/login/id-proofing')
  }, [router])

  const handleVerified = useCallback(() => {
    setPageData('docv_status', 'success')
    trackEvent(AnalyticsEvents.DOCV_RESULT)
    setPageData('idv_final_status', 'success')
    trackEvent(AnalyticsEvents.IDV_FINAL_RESULT)
    clearChallengeContext()
    router.push('/dashboard')
  }, [router, setPageData, trackEvent])

  const handleRejected = useCallback(
    (offboardingReason?: string) => {
      setPageData('docv_status', 'fail')
      trackEvent(AnalyticsEvents.DOCV_RESULT)
      setPageData('idv_final_status', 'fail')
      trackEvent(AnalyticsEvents.IDV_FINAL_RESULT)
      sessionStorage.setItem('offboarding_reason', offboardingReason ?? '')
      sessionStorage.setItem('offboarding_canApply', 'false')
      clearChallengeContext()
      router.push('/login/id-proofing/off-boarding')
    },
    [router, setPageData, trackEvent]
  )

  // Auto-transition from capture to pending after a delay. For remote phone
  // capture the SDK's onSuccess never fires (capture happens on a different
  // device), leaving the container blank. After 15 seconds the user has either
  // scanned the QR / opened the SMS link, so show the "checking" UI instead.
  useEffect(() => {
    if (subState !== 'capture') return

    const timerId = window.setTimeout(() => {
      sessionStorage.setItem(SK_SUB_STATE, 'pending')
      setSubState('pending')
    }, 15_000)

    return () => window.clearTimeout(timerId)
  }, [subState])

  // Safety net: if the webhook resolves the challenge while the SDK capture is
  // still active (e.g., SDK fires onSuccess late or not at all), redirect based
  // on the polled status rather than waiting for the pending sub-state.
  useEffect(() => {
    if (subState !== 'capture') return
    if (statusQuery.data?.status === 'verified') {
      handleVerified()
    } else if (statusQuery.data?.status === 'rejected') {
      handleRejected(statusQuery.data.offboardingReason ?? undefined)
    }
  }, [
    subState,
    statusQuery.data?.status,
    statusQuery.data?.offboardingReason,
    handleVerified,
    handleRejected
  ])

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        {error && (
          <Alert
            variant="error"
            slim
            className="margin-bottom-2"
          >
            {error}
          </Alert>
        )}

        {subState === 'interstitial' && challengeId && (
          <DocVerifyInterstitial
            allowIdRetry={allowIdRetry}
            isStartingChallenge={startChallenge.isPending}
            onContinue={handleContinue}
            onEnterIdNumber={handleEnterIdNumber}
            contactLink={contactLink}
          />
        )}

        {subState === 'capture' && captureLaunchConfig && (
          <DocVerifyCapture
            adapter={adapter}
            launchConfig={captureLaunchConfig}
          />
        )}

        {subState === 'pending' && challengeId && (
          <VerificationPending
            challengeId={challengeId}
            onVerified={handleVerified}
            onRejected={handleRejected}
          />
        )}
      </div>
    </div>
  )
}
