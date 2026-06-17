'use client'

import { apiFetch } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema,
  redirectToOidcOffBoarding
} from '@/features/auth/api/oidc'
import { hasIal1Plus, isIdProofingCompletionFresh } from '@/lib/jwt'
import { getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * OIDC callback: the IdP redirects here with ?code=...&state=...
 * We send code + state to the .NET /api/auth/oidc/callback endpoint, which
 * uses the server-side pre-auth session (code_verifier, stateCode, isStepUp)
 * to exchange with the IdP. We then send the callbackToken to
 * /api/auth/oidc/complete-login to create the portal session.
 *
 * All flow metadata (stateCode, isStepUp, returnUrl) is stored in the server-side
 * pre-auth session — no sessionStorage is used.
 *
 * IdP error redirects (?error=) always go to off-boarding immediately, including when the
 * visitor already has a portal session (step-up denied, back-button into PingOne, etc.).
 * Other failures (missing params when logged out, token exchange error) also use
 * {@link OIDC_CALLBACK_ERROR_OFF_BOARDING}.
 *
 * Back-button re-entry: authenticated visitors with no OAuth params, or who already
 * completed step-up (IAL1+ with a fresh proofing window), skip exchange so stale codes
 * are not treated as failure. In-progress step-up still runs exchange when code and state
 * are present, even if a portal session already exists from the initial sign-in.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { session, isAuthenticated, isLoading, login } = useAuth()
  const { t } = useTranslation('login')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const exchangeStartedRef = useRef(false)
  const isCO = getState() === 'co'

  const isProofedReEntry = useMemo(
    () => hasIal1Plus(session) && isIdProofingCompletionFresh(session),
    [session]
  )

  useEffect(() => {
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')
    const errorParam = params.get('error')

    // IdP errors do not require /auth/status; evaluate before isLoading and before
    // authenticated back-button shortcuts so step-up failures are not sent to /dashboard.
    if (errorParam) {
      redirectToOidcOffBoarding(router, {
        reason: 'idp_redirect',
        idpError: errorParam,
        idpErrorDescription: params.get('error_description') ?? undefined
      })
      return
    }

    if (isLoading) {
      return
    }

    if (isAuthenticated && (!code || !state)) {
      router.replace('/dashboard')
      return
    }

    if (isProofedReEntry) {
      router.replace('/dashboard')
      return
    }

    if (!code || !state) {
      redirectToOidcOffBoarding(router, {
        reason: 'missing_params',
        hasCode: Boolean(code),
        hasState: Boolean(state)
      })
      return
    }

    if (exchangeStartedRef.current) {
      return
    }

    exchangeStartedRef.current = true

    let cancelled = false
    async function run() {
      try {
        const { callbackToken } = await apiFetch('/auth/oidc/callback', {
          method: 'POST',
          body: { code, state },
          schema: OidcCallbackTokenResponseSchema
        })
        if (cancelled) {
          return
        }

        const response = await apiFetch('/auth/oidc/complete-login', {
          method: 'POST',
          body: { callbackToken },
          schema: OidcCompleteLoginResponseSchema
        })
        if (cancelled) {
          return
        }

        await login()
        const destination = response.returnUrl ?? '/dashboard'
        router.replace(destination)
      } catch {
        if (cancelled) {
          return
        }
        // API failures are logged server-side; only browser-only failures use report-failure.
        redirectToOidcOffBoarding(router)
      }
    }
    run()
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, isLoading, isProofedReEntry, login, router])

  if (isCO) {
    return (
      <CoLoadingScreen
        title={tProcessing('title', 'Please wait...')}
        message={tProcessing(
          'body',
          'Do not exit the page. Checking to see if we have enough information.'
        )}
      />
    )
  }

  return (
    <div className="usa-section">
      <div
        className="grid-container maxw-tablet"
        aria-live="polite"
        role="status"
      >
        <p className="font-sans-md">{t('callbackSigningIn')}</p>
      </div>
    </div>
  )
}
