'use client'

import { apiFetch } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema
} from '@/features/auth/api/oidc/schema'
import { getTranslations } from '@/lib/translations'
import { Alert, getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'

/**
 * OIDC callback: the IdP redirects here with ?code=...&state=...
 * We send code + state to the .NET /api/auth/oidc/callback endpoint, which
 * uses the server-side pre-auth session (code_verifier, stateCode, isStepUp)
 * to exchange with the IdP. We then send the callbackToken to
 * /api/auth/oidc/complete-login to create the portal session.
 *
 * All flow metadata (stateCode, isStepUp, returnUrl) is stored in the server-side
 * pre-auth session — no sessionStorage is used.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const t = getTranslations('login')
  const tProcessing = getTranslations('step-upProcessing')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
  const [errorDetail, setErrorDetail] = useState<string | null>(null)
  const exchangeStartedRef = useRef(false)
  const isCO = getState() === 'co'

  useEffect(() => {
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')
    const errorParam = params.get('error')
    const errorDescription = params.get('error_description')

    // IdP returned an error (e.g., user cancelled login).
    if (errorParam) {
      const idpDetail = errorDescription?.trim() ?? ''
      const portalLine = t('callbackErrorIdpRedirect', t('callbackErrorGeneric'))
      const message = idpDetail ? `${portalLine} ${idpDetail}` : portalLine
      queueMicrotask(() => {
        setErrorDetail(message)
        setStatus('error')
      })
      return
    }

    if (!code || !state) {
      queueMicrotask(() => {
        setErrorDetail(t('callbackErrorMissingParams'))
        setStatus('error')
      })
      return
    }

    if (exchangeStartedRef.current) return
    exchangeStartedRef.current = true

    let cancelled = false
    async function run() {
      try {
        // Send code + state to the server. The server reads stateCode, code_verifier,
        // isStepUp, and returnUrl from the pre-auth session (oidc_session cookie).
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

        // Backend set the HttpOnly session cookie; refresh the context from /auth/status.
        await login()
        const destination = response.returnUrl ?? '/dashboard'
        router.replace(destination)
      } catch (e) {
        const errMsg =
          e instanceof Error ? e.message : typeof e === 'string' ? e : t('callbackErrorGeneric')
        setErrorDetail(errMsg || t('callbackErrorGeneric'))
        if (!cancelled) {
          setStatus('error')
        }
      }
    }
    run()
    return () => {
      cancelled = true
      // React Strict Mode remounts effects: allow the next mount to run the exchange;
      // otherwise ref stays true and the retried effect bails while the aborted run skipped navigation.
      exchangeStartedRef.current = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- t (getTranslations) is a static lookup
  }, [login, router])

  useEffect(() => {
    if (status === 'error') {
      // Give user a moment to read the error before redirecting to login
      const timeout = setTimeout(() => router.replace('/login'), 5000)
      return () => clearTimeout(timeout)
    }
    return undefined
  }, [status, router])

  if (status === 'error') {
    return (
      <div className="usa-section">
        <div
          className="grid-container maxw-tablet"
          aria-live="polite"
          role="status"
        >
          <Alert
            variant="error"
            heading={t('callbackSignInIssue')}
          >
            {errorDetail}
          </Alert>
        </div>
      </div>
    )
  }

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
