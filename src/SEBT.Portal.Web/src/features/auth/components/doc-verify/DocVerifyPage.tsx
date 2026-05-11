'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert } from '@sebt/design-system'

import {
  clearChallengeContext,
  SK_CHALLENGE_ID,
  SK_STILL_CHECKING,
  SK_SUB_STATE,
  SubState
} from '@/features/auth/components/doc-verify/sessionKeys'
import {
  useRefreshToken,
  useResubmitChallenge,
  useStartChallenge,
  useVerificationStatus
} from '../../api'
import { DocVerifyInterstitial } from './DocVerifyInterstitial'
import { DocVerifyResubmit } from './DocVerifyResubmit'
import { VerificationPending } from './VerificationPending'

interface DocVerifyPageProps {
  contactLink: string
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

export function DocVerifyPage({ contactLink }: DocVerifyPageProps) {
  const router = useRouter()
  const searchParams = useSearchParams()
  const { t } = useTranslation('idProofing')
  const startChallenge = useStartChallenge()
  const resubmitChallenge = useResubmitChallenge()
  const refreshToken = useRefreshToken()

  const [subState, setSubState] = useState<SubState>('interstitial')
  const [challengeId, setChallengeId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { setPageData, trackEvent } = useDataLayer()

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

    // Seed challengeId from URL/sessionStorage after mount because sessionStorage
    // is not available during SSR — we can't lazy-init this via useState.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setChallengeId(ctx.challengeId)
    // Persist to sessionStorage for mobile tab recovery
    sessionStorage.setItem(SK_CHALLENGE_ID, ctx.challengeId)

    // Either persisted sub-state means the user already handed off to Socure
    // on a prior visit. Resume at pending and let polling pick up the webhook
    // outcome. The legacy 'capture' value may live in sessionStorage from an
    // older client build; map it forward rather than drop the session.
    if (ctx.subState === 'capture' || ctx.subState === 'pending') {
      setSubState('pending')
      sessionStorage.setItem(SK_SUB_STATE, 'pending')
    }
  }, [router, searchParams])

  // Poll status during interstitial so the DocVerifyInterstitial CTA can
  // expose the allowIdRetry flag. VerificationPending runs its own polling
  // once the user hands off to Socure.
  const statusQuery = useVerificationStatus(
    subState === 'interstitial' && challengeId ? challengeId : undefined
  )
  const allowIdRetry = statusQuery.data?.allowIdRetry ?? false

  // "Continue" → open Socure's hosted capture URL in a new tab. The blank tab
  // is opened synchronously to preserve the click's user-gesture activation;
  // browsers block window.open once an async boundary separates it from the
  // user event. Once the JIT token fetch resolves, redirect that tab to the
  // real Socure URL. A popup blocker collapses us to same-tab navigation.
  const handleContinue = () => {
    if (!challengeId) return
    setError(null)
    trackEvent(AnalyticsEvents.DOCV_START)

    const captureTab = window.open('about:blank', '_blank')

    startChallenge
      .mutateAsync(challengeId)
      .then(({ docvUrl }) => {
        if (captureTab && !captureTab.closed) {
          captureTab.location.href = docvUrl
        } else {
          window.location.href = docvUrl
        }
        sessionStorage.setItem(SK_SUB_STATE, 'pending')
        setSubState('pending')
      })
      .catch(() => {
        if (captureTab && !captureTab.closed) captureTab.close()
        setError(
          t(
            'docVerifyStartError',
            'Something went wrong starting document verification. Please try again.'
          )
        )
      })
  }

  const handleEnterIdNumber = useCallback(() => {
    clearChallengeContext()
    router.push('/login/id-proofing')
  }, [router])

  // Pin the latest mutateAsync through a ref so handleVerified's identity is
  // not affected by the mutation's internal status flips. Without this,
  // VerificationPending's effect (which depends on onVerified) would re-run
  // every time the refresh mutation's state changed, calling handleVerified
  // again and creating an infinite update loop.
  const refreshTokenAsyncRef = useRef(refreshToken.mutateAsync)
  useEffect(() => {
    refreshTokenAsyncRef.current = refreshToken.mutateAsync
  }, [refreshToken.mutateAsync])

  const handleVerified = useCallback(async () => {
    setPageData('docv_status', 'success')
    trackEvent(AnalyticsEvents.DOCV_RESULT)
    setPageData('idv_final_status', 'success')
    trackEvent(AnalyticsEvents.IDV_FINAL_RESULT)
    clearChallengeContext()

    // DC-296: the webhook just bumped the user to IAL1+ server-side. Await a
    // token refresh so the rotated HttpOnly cookie is in place before we
    // navigate. Otherwise the dashboard's first fetches race the refresh and
    // hit the IAL guard with the stale IAL1 JWT. Swallow failures so we never
    // trap the user on the "verified" screen; the dashboard will recover.
    try {
      await refreshTokenAsyncRef.current()
    } catch {
      // Intentionally silent. Fresh cookie did not arrive; proceed anyway.
    }

    router.push('/dashboard')
  }, [router, setPageData, trackEvent])

  const handleRejected = useCallback(
    (offboardingReason?: string) => {
      setPageData('docv_status', 'fail')
      trackEvent(AnalyticsEvents.DOCV_RESULT)
      setPageData('idv_final_status', 'fail')
      trackEvent(AnalyticsEvents.IDV_FINAL_RESULT)
      clearChallengeContext()
      // Pass the reason via URL so the off-boarding route can render distinct
      // copy (docVerificationFailed, challengeNotFound, etc). Mirrors the
      // pattern IdProofingForm uses for noIdProvided.
      const params = new URLSearchParams()
      if (offboardingReason) {
        params.set('reason', offboardingReason)
      }
      const query = params.toString()
      router.push(`/login/id-proofing/off-boarding${query ? `?${query}` : ''}`)
    },
    [router, setPageData, trackEvent]
  )

  const handleEnterResubmit = useCallback(() => {
    setSubState('resubmit')
    setError(null)
  }, [])

  // "Try again" → open Socure's hosted retry URL in a new tab. Same user-gesture
  // discipline as handleContinue: synchronous window.open so popup blockers honor
  // the click. The mutation creates a brand-new docv_stepup challenge server-side
  // and returns the new challenge's public ID; we swap that into URL, state, and
  // sessionStorage so polling and reloads target the fresh challenge — not the
  // old terminal Resubmit one (which would otherwise loop us right back here).
  const handleResubmit = () => {
    if (!challengeId) return
    setError(null)
    trackEvent(AnalyticsEvents.DOCV_RESUBMIT)

    const captureTab = window.open('about:blank', '_blank')

    resubmitChallenge
      .mutateAsync(challengeId)
      .then(({ challengeId: newChallengeId, docvUrl }) => {
        if (captureTab && !captureTab.closed) {
          captureTab.location.href = docvUrl
        } else {
          window.location.href = docvUrl
        }
        // Clear "still checking" so the new challenge's pending state starts fresh
        sessionStorage.removeItem(SK_STILL_CHECKING)
        sessionStorage.setItem(SK_CHALLENGE_ID, newChallengeId)
        sessionStorage.setItem(SK_SUB_STATE, 'pending')
        setChallengeId(newChallengeId)
        setSubState('pending')
        router.replace(`/login/id-proofing/doc-verify?challengeId=${newChallengeId}`)
      })
      .catch(() => {
        if (captureTab && !captureTab.closed) captureTab.close()
        setError(
          t('docVerifyResubmitError', "We couldn't start a retry. Please try again in a moment.")
        )
      })
  }

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

        {subState === 'pending' && challengeId && (
          <VerificationPending
            challengeId={challengeId}
            onVerified={handleVerified}
            onRejected={handleRejected}
            onResubmit={handleEnterResubmit}
          />
        )}

        {subState === 'resubmit' && challengeId && (
          <DocVerifyResubmit
            onResubmit={handleResubmit}
            isResubmitting={resubmitChallenge.isPending}
          />
        )}
      </div>
    </div>
  )
}
