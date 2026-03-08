'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useRef, useState } from 'react'

import { Alert } from '@/components/ui'

import { useStartChallenge, useVerificationStatus } from '../../api'
import { createDocVAdapter, type DocVAdapter, type DocVAdapterConfig } from './adapters'
import { DocVerifyCapture } from './DocVerifyCapture'
import { DocVerifyInterstitial } from './DocVerifyInterstitial'
import { VerificationPending } from './VerificationPending'

// SessionStorage keys for challenge state persistence (D6)
const SK_CHALLENGE_ID = 'docVerify_challengeId'
const SK_SUB_STATE = 'docVerify_subState'

type SubState = 'interstitial' | 'capture' | 'pending'

interface DocVerifyPageProps {
  contactLink: string
  sdkKey: string
}

function clearChallengeContext(): void {
  sessionStorage.removeItem(SK_CHALLENGE_ID)
  sessionStorage.removeItem(SK_SUB_STATE)
}

export function DocVerifyPage({ contactLink, sdkKey }: DocVerifyPageProps) {
  const router = useRouter()
  const searchParams = useSearchParams()
  const startChallenge = useStartChallenge()

  const [subState, setSubState] = useState<SubState>('interstitial')
  const [challengeId, setChallengeId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  // Read challengeId from URL query param (primary) or sessionStorage (fallback for mobile recovery)
  useEffect(() => {
    const urlChallengeId = searchParams.get('challengeId')
    const storedChallengeId = sessionStorage.getItem(SK_CHALLENGE_ID)
    const resolvedId = urlChallengeId || storedChallengeId

    if (!resolvedId) {
      // No challenge context — redirect to id-proofing form
      router.replace('/login/id-proofing')
      return
    }

    setChallengeId(resolvedId)

    // Persist to sessionStorage if resolved from URL (mobile recovery needs it)
    if (urlChallengeId && !storedChallengeId) {
      sessionStorage.setItem(SK_CHALLENGE_ID, urlChallengeId)
    }

    // If the user was in capture (e.g., mobile tab recovery), skip to pending (D6)
    const persisted = sessionStorage.getItem(SK_SUB_STATE)
    if (persisted === 'capture' || persisted === 'pending') {
      adapter.reset()
      setSubState('pending')
      sessionStorage.setItem(SK_SUB_STATE, 'pending')
    }
  }, [searchParams, router, adapter])

  // Fetch verification status during interstitial to get allowIdRetry (D9: server-derived)
  // VerificationPending handles its own polling when in the pending sub-state
  const { data: statusData } = useVerificationStatus(
    subState === 'interstitial' && challengeId ? challengeId : undefined
  )
  const allowIdRetry = statusData?.allowIdRetry ?? false

  // "Continue" click handler — JIT token fetch, then transition to capture sub-state.
  // The actual adapter.launch() happens inside DocVerifyCapture after its container mounts.
  const handleContinue = async () => {
    if (!challengeId) return
    setError(null)

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
      // TODO: Use t('docVerify.errorStartChallenge') once key is available in dc.csv
      setError('Something went wrong starting document verification. Please try again.')
    }
  }

  const handleEnterIdNumber = useCallback(() => {
    clearChallengeContext()
    router.push('/login/id-proofing')
  }, [router])

  const handleVerified = useCallback(() => {
    clearChallengeContext()
    router.push('/dashboard')
  }, [router])

  const handleRejected = useCallback(
    (offboardingReason?: string) => {
      sessionStorage.setItem('offboarding_reason', offboardingReason ?? '')
      sessionStorage.setItem('offboarding_canApply', 'false')
      clearChallengeContext()
      router.push('/login/id-proofing/off-boarding')
    },
    [router]
  )

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
