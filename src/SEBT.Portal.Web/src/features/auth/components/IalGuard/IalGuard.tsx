'use client'

import { apiFetch } from '@/api'
import { OidcConfigResponseSchema } from '@/features/auth/api/oidc/schema'
import { getAuthToken } from '@/features/auth/context'
import { hasIal1Plus } from '@/lib/jwt'
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
  /** Minimum IAL required (default IAL1plus). Only enforced for CO. */
  requiredIal?: typeof STEP_UP_REQUIRED_IAL
}

/**
 * For CO users, redirects to MyColorado step-up when IAL is below required.
 * DC users are not affected (they use OTP + Socure).
 * After a successful step-up, the portal JWT includes ial `1plus` or `2`, so hasIal1Plus is true and the redirect does not run again.
 */
export function IalGuard({ children, requiredIal = STEP_UP_REQUIRED_IAL }: IalGuardProps) {
  const isCo = getState() === 'co'
  const token = getAuthToken()
  const passesWithoutStepUp = !isCo || !token || (requiredIal === 'IAL1plus' && hasIal1Plus(token))

  const [stepUpError, setStepUpError] = useState(false)

  useEffect(() => {
    if (passesWithoutStepUp) {
      return
    }

    let cancelled = false
    async function startStepUp() {
      try {
        const config = await apiFetch(`/auth/oidc/co/config?stepUp=true`, {
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
