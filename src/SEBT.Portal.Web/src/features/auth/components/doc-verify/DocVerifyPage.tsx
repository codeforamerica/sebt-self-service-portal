'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Alert } from '@sebt/design-system'

import {
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
  const challengeId = searchParams.get('challengeId') || sessionStorage.getItem(SK_CHALLENGE_ID)
  const persisted = sessionStorage.getItem(SK_SUB_STATE)
  const subState = persisted === 'capture' || persisted === 'pending' ? persisted : null
  return { challengeId, subState }
}

function clearChallengeContext(): void {
  sessionStorage.removeItem(SK_CHALLENGE_ID)
  sessionStorage.removeItem(SK_SUB_STATE)
}

export function DocVerifyPage({ contactLink, sdkKey }: DocVerifyPageProps) {
  const router = useRouter()
  const searchParams = useSearchParams()
  const { t } = useTranslation('idProofing')
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

  // Read challenge context on mount — URL query param is primary, sessionStorage is fallback (D6, D9)
  useEffect(() => {
    const ctx = readChallengeContext(searchParams)

    if (!ctx.challengeId) {
      // No challenge context — redirect to id-proofing form
      router.replace('/login/id-proofing')
      return
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

  // Derive allowIdRetry from status API (D9) — server is the authority
  const statusQuery = useVerificationStatus(
    subState === 'interstitial' && challengeId ? challengeId : undefined
  )
  const allowIdRetry = statusQuery.data?.allowIdRetry ?? false

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
