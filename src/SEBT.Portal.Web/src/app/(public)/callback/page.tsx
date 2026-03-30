'use client'

import { apiFetch } from '@/api'
import {
  OidcCallbackTokenResponseSchema,
  OidcCompleteLoginResponseSchema,
  setAuthToken,
  useAuth
} from '@/features/auth'
import { clearPkceStorage, getPkceFromStorage } from '@/lib/oidc-pkce'
import { getTranslations } from '@/lib/translations'
import { Alert, getState } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'

/**
 * OIDC callback: state IdP redirects here with ?code=...&state=...
 * We then send callbackToken to the .NET complete-login endpoint to create session and get the portal JWT.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const t = getTranslations('login')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
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
        setStatus('error')
      })
      return
    }

    if (exchangeStartedRef.current) return
    exchangeStartedRef.current = true

    let cancelled = false
    async function run() {
      const stored = getPkceFromStorage()
      if (!stored) {
        clearPkceStorage()
        if (!cancelled) {
          setErrorDetail(t('callbackErrorSessionExpired'))
          setStatus('error')
        }
        return
      }
      if (stored.state !== state) {
        clearPkceStorage()
        if (!cancelled) {
          setErrorDetail(t('callbackErrorStateMismatch'))
          setStatus('error')
        }
        return
      }

      const isStepUp = stored.isStepUp === true
      clearPkceStorage()

      try {
        const returnUrl = stored.returnUrl ?? ''

        // Exchange authorization code for a short-lived callback token (via Next.js server).
        // Pass redirect_uri from PKCE storage — must match exactly what was sent in the auth request.
        const { callbackToken } = await apiFetch('/auth/oidc/callback', {
          method: 'POST',
          body: {
            code,
            code_verifier: stored.code_verifier,
            redirectUri: stored.redirect_uri,
            state,
            stateCode: getState(),
            isStepUp
          },
          schema: OidcCallbackTokenResponseSchema
        })
        if (cancelled) return

        // Complete login with .NET backend — validates callback token, creates portal JWT
        const response = await apiFetch('/auth/oidc/complete-login', {
          method: 'POST',
          body: {
            stateCode: getState(),
            callbackToken,
            isStepUp,
            returnUrl: returnUrl || undefined
          },
          schema: OidcCompleteLoginResponseSchema
        })
        if (cancelled) return

        const { token, returnUrl: resolvedReturnUrl } = response

        // Persist to sessionStorage and notify auth context
        setAuthToken(token)
        login(token)
        // Ensure token is stored and listeners have run before navigating
        await new Promise((resolve) => setTimeout(resolve, 0))
        const destination = isStepUp && resolvedReturnUrl ? resolvedReturnUrl : '/dashboard'
        router.replace(destination)
      } catch {
        if (!cancelled) {
          setErrorDetail(t('callbackErrorGeneric'))
          setStatus('error')
        }
      }
    }
    run()
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- t (getTranslations) is a static lookup, not a reactive dependency
  }, [login, router])

  useEffect(() => {
    if (status === 'error') {
      // Give user a moment to read the error before redirecting to login
      const timeout = setTimeout(() => router.replace('/login'), 5000)
      return () => clearTimeout(timeout)
    }
    return undefined
  }, [status, router])

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
          <p className="font-sans-md">{t('callbackSigningIn')}</p>
        )}
      </div>
    </div>
  )
}
