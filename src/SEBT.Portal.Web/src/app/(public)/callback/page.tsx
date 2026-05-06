'use client'

import { ApiError, apiFetch } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import {
  OIDC_CALLBACK_ERROR_OFF_BOARDING,
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema
} from '@/features/auth/api/oidc'
import { getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef } from 'react'
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
 * Any failure (IdP error redirect, missing params, token exchange error) sends the user
 * to id-proofing off-boarding with {@link OIDC_CALLBACK_ERROR_OFF_BOARDING}.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const { t } = useTranslation('login')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const exchangeStartedRef = useRef(false)
  const isCO = getState() === 'co'

  useEffect(() => {
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')
    const errorParam = params.get('error')

    if (errorParam) {
      router.replace(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      return
    }

    if (!code || !state) {
      router.replace(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      return
    }

    if (exchangeStartedRef.current) return
    exchangeStartedRef.current = true

    let cancelled = false
    async function run() {
      try {
        const { callbackToken } = await apiFetch('/auth/oidc/callback', {
          method: 'POST',
          body: { code, state },
          schema: OidcCallbackTokenResponseSchema
        })
        if (cancelled) return

        const response = await apiFetch('/auth/oidc/complete-login', {
          method: 'POST',
          body: { callbackToken },
          schema: OidcCompleteLoginResponseSchema
        })
        if (cancelled) return

        await login()
        const destination = response.returnUrl ?? '/dashboard'
        router.replace(destination)
      } catch (e) {
        if (cancelled) return
        const statusCode = e instanceof ApiError ? e.status : undefined
        const logDetail = e instanceof Error ? e.message : ''
        if (process.env.NODE_ENV === 'development') {
          console.warn('[callback] OIDC exchange failed', {
            statusCode,
            detail: logDetail.slice(0, 500)
          })
        }
        router.replace(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      }
    }
    run()
    return () => {
      cancelled = true
      exchangeStartedRef.current = false
    }
  }, [login, router])

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
