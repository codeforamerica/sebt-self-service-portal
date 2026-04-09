'use client'

import { apiFetch } from '@/api'
import { useAuth } from '@/features/auth'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema
} from '@/features/auth/api/oidc/schema'
import { clearPkceStorage, getPkceFromStorage } from '@/lib/oidc-pkce'
import { getTranslations } from '@/lib/translations'
import { Alert, getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'

type CallbackStep = 'loading' | 'have_code_state' | 'have_pkce' | 'exchanging' | 'error'

/**
 * OIDC callback: state IdP redirects here with ?code=...&state=...
 * We send code + code_verifier to Next.js /api/auth/oidc/callback; it exchanges with IdP, returns callbackToken.
 * We then send callbackToken to the .NET complete-login endpoint to create session and get the portal JWT.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const t = getTranslations('login')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
  const [step, setStep] = useState<CallbackStep>('loading')
  const [errorDetail, setErrorDetail] = useState<string | null>(null)
  const exchangeStartedRef = useRef(false)

  useEffect(() => {
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')
    const errorParam = params.get('error')
    const errorDescription = params.get('error_description')

    if (errorParam) {
      const storedPkce = getPkceFromStorage()
      const stepUpFromIdpError = storedPkce?.isStepUp === true
      const idpDetail = errorDescription?.trim() ?? ''
      const portalLine = t(
        stepUpFromIdpError ? 'callbackErrorStepUpFailed' : 'callbackErrorIdpRedirect',
        t('callbackErrorGeneric')
      )
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
        setStep('error')
        setStatus('error')
      })
      return
    }
    queueMicrotask(() => setStep('have_code_state'))

    if (exchangeStartedRef.current) return
    exchangeStartedRef.current = true

    let cancelled = false
    async function run() {
      const stored = getPkceFromStorage()
      if (!stored) {
        setErrorDetail(t('callbackErrorSessionExpired'))
        clearPkceStorage()
        if (!cancelled) {
          setStep('error')
          setStatus('error')
        }
        return
      }
      if (stored.state !== state) {
        setErrorDetail(t('callbackErrorStateMismatch'))
        clearPkceStorage()
        if (!cancelled) {
          setStep('error')
          setStatus('error')
        }
        return
      }
      setStep('have_pkce')
      const isStepUp = stored.isStepUp === true
      const returnUrl = stored.returnUrl ?? ''
      clearPkceStorage()

      try {
        setStep('exchanging')
        const stateCode = getState()

        const { callbackToken } = await apiFetch('/auth/oidc/callback', {
          method: 'POST',
          body: {
            code,
            code_verifier: stored.code_verifier,
            redirectUri: stored.redirect_uri,
            state,
            stateCode,
            isStepUp
          },
          schema: OidcCallbackTokenResponseSchema
        })
        if (cancelled) return

        const response = await apiFetch('/auth/oidc/complete-login', {
          method: 'POST',
          body: {
            stateCode,
            callbackToken,
            isStepUp,
            returnUrl: returnUrl || undefined
          },
          schema: OidcCompleteLoginResponseSchema
        })
        if (cancelled) return

        // Backend set the HttpOnly session cookie; refresh the context from /auth/status.
        await login()
        const destination = isStepUp && response.returnUrl ? response.returnUrl : '/dashboard'
        router.replace(destination)
      } catch (e) {
        const errMsg =
          e instanceof Error ? e.message : typeof e === 'string' ? e : t('callbackErrorGeneric')
        setErrorDetail(errMsg || t('callbackErrorGeneric'))
        if (!cancelled) {
          setStep('error')
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

  const stepMessage: Record<CallbackStep, string> = {
    loading: t('callbackSigningIn'),
    have_code_state: t('callbackSigningIn'),
    have_pkce: t('callbackSigningIn'),
    exchanging: t('callbackSigningIn'),
    error: errorDetail ?? t('callbackErrorGeneric')
  }

  return (
    <div className="usa-section">
      <div
        className="grid-container maxw-tablet"
        aria-live="polite"
        role="status"
      >
        {status === 'error' ? (
          <Alert
            variant="error"
            heading={t('callbackSignInIssue')}
          >
            {errorDetail}
          </Alert>
        ) : (
          <p className="font-sans-md">{stepMessage[step]}</p>
        )}
      </div>
    </div>
  )
}
