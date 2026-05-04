'use client'

import { apiFetch } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema
} from '@/features/auth/api/oidc/schema'
import { Alert, getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
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
 */
type ErrorState = { kind: 'key'; key: string; appended?: string } | { kind: 'raw'; message: string }

export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const { t } = useTranslation('login')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
  // Store the i18n key (not the resolved string) so a mid-flow language
  // toggle re-translates the error on render. `appended` carries IdP-supplied
  // detail text that we cannot translate (passed through as-is). `raw` covers
  // server/network errors whose message is not translatable.
  const [error, setError] = useState<ErrorState | null>(null)
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
      const idpDetail = errorDescription?.trim()
      queueMicrotask(() => {
        setError({
          kind: 'key',
          key: 'callbackErrorIdpRedirect',
          ...(idpDetail ? { appended: idpDetail } : {})
        })
        setStatus('error')
      })
      return
    }

    if (!code || !state) {
      queueMicrotask(() => {
        setError({ kind: 'key', key: 'callbackErrorMissingParams' })
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
        if (cancelled) return
        const rawMessage = e instanceof Error ? e.message : typeof e === 'string' ? e : ''
        if (rawMessage) {
          setError({ kind: 'raw', message: rawMessage })
        } else {
          setError({ kind: 'key', key: 'callbackErrorGeneric' })
        }
        setStatus('error')
      }
    }
    run()
    return () => {
      cancelled = true
      // React Strict Mode remounts effects: allow the next mount to run the exchange;
      // otherwise ref stays true and the retried effect bails while the aborted run skipped navigation.
      exchangeStartedRef.current = false
    }
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
    let body: string | null = null
    if (error?.kind === 'key') {
      const line = t(error.key, t('callbackErrorGeneric'))
      body = error.appended ? `${line} ${error.appended}` : line
    } else if (error?.kind === 'raw') {
      body = error.message
    }
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
            {body}
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
