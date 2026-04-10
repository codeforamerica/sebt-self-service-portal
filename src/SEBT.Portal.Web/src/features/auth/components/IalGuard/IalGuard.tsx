'use client'

import { apiFetch } from '@/api'
import { OidcConfigResponseSchema } from '@/features/auth/api/oidc/schema'
import { useAuth } from '@/features/auth/context'
import { getCoIdProofingMaxAgeYearsRaw, isDebugRepeatOidcStepUp } from '@/lib/ial-guard-config'
import { hasIal1Plus, isIdProofingCompletionFresh, parseIdProofingMaxAgeYears } from '@/lib/jwt'
import {
  buildAuthorizationUrl,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  getOidcRedirectUriForCurrentOrigin,
  savePkceForCallback
} from '@/lib/oidc-pkce'
import { Button, getState, SummaryBox } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

const STEP_UP_REQUIRED_IAL = 'IAL1plus' as const

/** Minimum time to show the “checking” screen so the flow never flashes straight to the challenge. */
const MIN_CHECKING_MS = 500

type GuardPhase = 'challenge' | 'redirecting' | 'error'

interface IalGuardProps {
  children: ReactNode
  /** Minimum IAL required (default IAL1plus). Enforced for routes that mount this guard. */
  requiredIal?: typeof STEP_UP_REQUIRED_IAL
}

async function startOidcStepUpRedirect(): Promise<void> {
  const stateCode = getState()
  const config = await apiFetch(`/auth/oidc/${stateCode}/config?stepUp=true`, {
    schema: OidcConfigResponseSchema
  })

  const codeVerifier = generateCodeVerifier()
  const codeChallenge = await generateCodeChallenge(codeVerifier)
  const stateValue = generateState()
  const returnUrl =
    typeof window !== 'undefined' ? window.location.pathname + window.location.search : '/dashboard'

  const redirectUri = getOidcRedirectUriForCurrentOrigin()
  savePkceForCallback(stateValue, codeVerifier, {
    redirectUri,
    tokenEndpoint: config.tokenEndpoint,
    clientId: config.clientId,
    isStepUp: true,
    returnUrl
  })

  const authUrl = buildAuthorizationUrl({ ...config, redirectUri }, codeChallenge, stateValue)
  window.location.href = authUrl
}

/**
 * Colorado OIDC step-up gate: brief “checking” UI, then an explicit challenge screen before redirect.
 * Mount only on routes that need this gate; the authenticated layout does not wrap the whole app.
 * `NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP=true` forces the challenge path in development even when the JWT already has IAL1+.
 */
export function IalGuard({ children, requiredIal = STEP_UP_REQUIRED_IAL }: IalGuardProps) {
  const { session } = useAuth()
  const router = useRouter()
  const { t } = useTranslation('common')
  const { t: tStepUpFailure } = useTranslation('stepUpFailure')

  const useOidcStepUpGate = getState() === 'co'
  const debugRepeatOidcStepUp = isDebugRepeatOidcStepUp()
  const maxIdProofingAgeYears = parseIdProofingMaxAgeYears(getCoIdProofingMaxAgeYearsRaw())

  const ialAndIdProofingSufficient =
    requiredIal === 'IAL1plus' &&
    hasIal1Plus(session) &&
    isIdProofingCompletionFresh(session, maxIdProofingAgeYears) &&
    !debugRepeatOidcStepUp

  const passesWithoutStepUp = !useOidcStepUpGate || !session || ialAndIdProofingSufficient

  const needsChallengeFlow = useOidcStepUpGate && !!session && !ialAndIdProofingSufficient

  const [phase, setPhase] = useState<GuardPhase | null>(null)

  useEffect(() => {
    if (!needsChallengeFlow) {
      return
    }

    const id = window.setTimeout(() => {
      setPhase('challenge')
    }, MIN_CHECKING_MS)

    return () => {
      window.clearTimeout(id)
    }
  }, [needsChallengeFlow])

  const handleBack = useCallback(() => {
    if (typeof window !== 'undefined' && window.history.length > 1) {
      router.back()
    } else {
      router.push('/dashboard')
    }
  }, [router])

  const handleVerify = useCallback(async () => {
    setPhase('redirecting')
    try {
      await startOidcStepUpRedirect()
    } catch {
      setPhase('error')
    }
  }, [])

  const checkingCopy = useMemo(
    () => ({
      title: t('ialGuardCheckingTitle', 'Please wait…'),
      body: t(
        'ialGuardCheckingBody',
        'Do not exit the page. Checking to see if we have enough information.'
      )
    }),
    [t]
  )

  if (passesWithoutStepUp) {
    return <>{children}</>
  }

  if (phase === 'error') {
    return (
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section aria-labelledby="ial-guard-error-title">
            <h1
              id="ial-guard-error-title"
              className="font-heading-lg text-primary margin-bottom-3 line-height-sans-1"
            >
              {tStepUpFailure(
                'title',
                "We're sorry, we aren't able to show your Summer EBT information"
              )}
            </h1>
            <p className="font-sans-sm margin-bottom-3">
              {tStepUpFailure('body', 'You can contact us if you need more help.')}
            </p>
            <div className="display-flex flex-row flex-wrap flex-gap-2 margin-top-3">
              <Button
                type="button"
                variant="outline"
                className="border-primary text-primary"
                onClick={handleBack}
              >
                {t('ialGuardBack', 'Back')}
              </Button>
              <Button
                type="button"
                variant="primary"
                className="bg-primary-dark text-white border-primary-dark"
                onClick={handleBack}
              >
                {tStepUpFailure('continue', 'Continue')}
              </Button>
            </div>
          </section>
        </div>
      </div>
    )
  }

  const showChecking = phase === null || phase === 'redirecting'
  const showChallenge = phase === 'challenge'

  if (showChecking) {
    return (
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section
            aria-busy="true"
            aria-labelledby="ial-guard-checking-title"
          >
            <h1
              id="ial-guard-checking-title"
              className="font-heading-lg text-primary margin-bottom-3 line-height-sans-1"
            >
              {checkingCopy.title}
            </h1>
            <div
              role="status"
              aria-live="polite"
            >
              <SummaryBox>
                <p className="font-sans-sm margin-0">{checkingCopy.body}</p>
              </SummaryBox>
            </div>
          </section>
        </div>
      </div>
    )
  }

  if (showChallenge) {
    return (
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section aria-labelledby="ial-guard-challenge-title">
            <h1
              id="ial-guard-challenge-title"
              className="font-heading-lg text-primary margin-bottom-3 line-height-sans-1"
            >
              {t(
                'ialGuardChallengeTitle',
                'To keep your account safe, we need to confirm it’s really you'
              )}
            </h1>
            <p className="font-sans-sm margin-bottom-3">
              {t(
                'ialGuardChallengeBody',
                'We need to share some information with our third-party vendor to verify your identity. We will do this only once and will not share or save anything without your permission.'
              )}
            </p>
            <div className="display-flex flex-row flex-wrap flex-gap-2 margin-top-3">
              <Button
                type="button"
                variant="outline"
                className="border-primary text-primary"
                onClick={handleBack}
              >
                {t('ialGuardBack', 'Back')}
              </Button>
              <Button
                type="button"
                variant="primary"
                className="bg-primary-dark text-white border-primary-dark"
                onClick={() => {
                  void handleVerify()
                }}
              >
                {t('ialGuardVerify', 'Verify')}
              </Button>
            </div>
          </section>
        </div>
      </div>
    )
  }

  return null
}
