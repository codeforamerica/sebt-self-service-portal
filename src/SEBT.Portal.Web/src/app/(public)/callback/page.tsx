'use client'

import { ApiError, apiFetch } from '@/api'
import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema
} from '@/features/auth/api/oidc/schema'
import { Alert, Button, getState, SummaryBox } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { classifyIdpOAuthRedirectError } from './oidcCallbackErrors'

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
type CallbackErrorState =
  | { kind: 'stepUpDeclined' }
  | { kind: 'key'; key: string; appended?: string }

export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const { t } = useTranslation('login')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
  const [error, setError] = useState<CallbackErrorState | null>(null)
  const exchangeStartedRef = useRef(false)
  const isCO = getState() === 'co'

  useEffect(() => {
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')
    const errorParam = params.get('error')
    const errorDescription = params.get('error_description')

    // IdP returned an error (e.g., user cancelled login / declined Socure consent).
    if (errorParam) {
      const classification = classifyIdpOAuthRedirectError(errorParam, errorDescription)
      queueMicrotask(() => {
        if (classification.type === 'stepUpDeclined') {
          setError({ kind: 'stepUpDeclined' })
        } else {
          setError({
            kind: 'key',
            key: 'callbackErrorIdpRedirect',
            ...(classification.safeDetail ? { appended: classification.safeDetail } : {})
          })
        }
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
        // Never surface raw API payloads (ProblemDetails, IdP blobs) on this screen.
        const statusCode = e instanceof ApiError ? e.status : undefined
        const logDetail = e instanceof Error ? e.message : ''
        if (process.env.NODE_ENV === 'development') {
          console.warn('[callback] OIDC exchange failed', {
            statusCode,
            detail: logDetail.slice(0, 500)
          })
        }
        setError({ kind: 'key', key: 'callbackErrorGeneric' })
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
    if (status !== 'error' || error?.kind === 'stepUpDeclined') {
      return undefined
    }
    // Brief pause so users can read IdP / exchange errors before continuing.
    const timeout = setTimeout(() => router.replace('/dashboard'), 5000)
    return () => clearTimeout(timeout)
  }, [status, error?.kind, router])

  if (status === 'error' && error?.kind === 'stepUpDeclined') {
    const title = t('callbackStepUpDeclinedTitle') || 'Identity verification was not completed'
    const body =
      t('callbackStepUpDeclinedBody') ||
      'You can go to your dashboard and try again when you are ready.'
    const actionLabel = t('callbackStepUpDeclinedActionDashboard') || 'Go to dashboard'

    return (
      <div className="usa-section">
        <div
          className="grid-container maxw-tablet"
          aria-live="polite"
          role="status"
        >
          <section aria-labelledby="callback-step-up-declined-title">
            <h1
              id="callback-step-up-declined-title"
              className="font-heading-lg text-primary margin-bottom-3 line-height-sans-1"
            >
              {title}
            </h1>
            <div
              role="status"
              aria-live="polite"
            >
              <SummaryBox>
                <p className="font-sans-sm margin-0">{body}</p>
              </SummaryBox>
            </div>
            <Button
              type="button"
              variant="primary"
              className="bg-primary-dark text-white border-primary-dark margin-top-3"
              onClick={() => router.replace('/dashboard')}
            >
              {actionLabel}
            </Button>
          </section>
        </div>
      </div>
    )
  }

  if (status === 'error') {
    let body: string | null = null
    if (error?.kind === 'key') {
      const line = t(error.key) || t('callbackErrorGeneric') || 'Something went wrong.'
      body = error.appended ? `${line} ${error.appended}` : line
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
            heading={t('callbackSignInIssue') || 'Sign-in issue'}
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
