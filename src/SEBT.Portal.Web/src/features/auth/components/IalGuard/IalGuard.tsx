'use client'

import { apiFetch } from '@/api'
import { env } from '@/env'
import { OidcConfigResponseSchema } from '@/features/auth/api/oidc/schema'
import { getAuthToken } from '@/features/auth/context'
import { hasIal1Plus, isIdProofingCompletionFresh, parseIdProofingMaxAgeYears } from '@/lib/jwt'
import {
  buildStepUpAuthorizationUrl,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  getOidcRedirectUriForCurrentOrigin,
  savePkceForCallback
} from '@/lib/oidc-pkce'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'
import { useEffect, useState, type ReactNode } from 'react'

const STEP_UP_REQUIRED_IAL = 'IAL1plus' as const

interface IalGuardProps {
  children: ReactNode
  /** Minimum IAL required (default IAL1plus). Enforced for routes that mount this guard. */
  requiredIal?: typeof STEP_UP_REQUIRED_IAL
}

/**
 * Redirects to OIDC step-up when IAL is below required or ID proofing completion (`id_proofing_completed_at`) is older than configured.
 * Mount only on routes that need this gate; the authenticated layout does not wrap the whole app.
 * After a successful step-up, the portal JWT includes ial `1plus` or `2` and `id_proofing_completed_at` from the API.
 * `NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP=true` forces step-up on every load for testing.
 */
export function IalGuard({ children, requiredIal = STEP_UP_REQUIRED_IAL }: IalGuardProps) {
  const useOidcStepUpGate = getState() === 'co'
  const token = getAuthToken()
  const debugRepeatOidcStepUp =
    process.env.NODE_ENV === 'development' && env.NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP === 'true'
  const maxIdProofingAgeYears = parseIdProofingMaxAgeYears(
    env.NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS
  )
  const ialAndIdProofingSufficient =
    requiredIal === 'IAL1plus' &&
    hasIal1Plus(token) &&
    isIdProofingCompletionFresh(token, maxIdProofingAgeYears) &&
    !debugRepeatOidcStepUp
  const passesWithoutStepUp = !useOidcStepUpGate || !token || ialAndIdProofingSufficient

  const [stepUpError, setStepUpError] = useState(false)

  useEffect(() => {
    if (passesWithoutStepUp) {
      return
    }

    let cancelled = false
    async function startStepUp() {
      try {
        const stateCode = getState()
        const config = await apiFetch(`/auth/oidc/${stateCode}/config?stepUp=true`, {
          schema: OidcConfigResponseSchema
        })
        if (cancelled) return

        const codeVerifier = generateCodeVerifier()
        const codeChallenge = await generateCodeChallenge(codeVerifier)
        const stateValue = generateState()
        const returnUrl =
          typeof window !== 'undefined'
            ? window.location.pathname + window.location.search
            : '/dashboard'

        const redirectUri = getOidcRedirectUriForCurrentOrigin()
        savePkceForCallback(stateValue, codeVerifier, {
          redirectUri,
          tokenEndpoint: config.tokenEndpoint,
          clientId: config.clientId,
          isStepUp: true,
          returnUrl
        })

        const { acrValues: rawAcr, ...configRest } = config
        const authUrl = buildStepUpAuthorizationUrl(
          {
            ...configRest,
            redirectUri,
            ...(rawAcr != null && rawAcr !== '' ? { acrValues: rawAcr } : {})
          },
          codeChallenge,
          stateValue
        )
        window.location.href = authUrl
      } catch {
        if (!cancelled) {
          setStepUpError(true)
        }
      }
    }

    void startStepUp()
    return () => {
      cancelled = true
    }
  }, [passesWithoutStepUp, requiredIal])

  if (passesWithoutStepUp) {
    return <>{children}</>
  }

  if (stepUpError) {
    const t = getTranslations('login')
    return (
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <p className="font-sans-md">
            {t(
              'stepUpVerificationRequired',
              'Additional verification is required to view this page. Please try again or contact support if the problem persists.'
            )}
          </p>
        </div>
      </div>
    )
  }

  /* step-up in progress: redirect imminent or awaiting config */
  return null
}
