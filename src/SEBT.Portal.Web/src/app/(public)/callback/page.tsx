'use client'

import { Alert } from '@/components/ui'
import { useAuth } from '@/features/auth'
import { clearPkceStorage, getPkceFromStorage } from '@/lib/oidc-pkce'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

type CallbackStep = 'loading' | 'have_code_state' | 'have_pkce' | 'exchanging' | 'error'

/**
 * OIDC callback: state IdP redirects here with ?code=...&state=...
 * We send code + code_verifier to the backend; backend exchanges with the IdP (using client secret) and returns the portal JWT.
 */
export default function CallbackPage() {
  const router = useRouter()
  const { login } = useAuth()
  const t = getTranslations('login')
  const [status, setStatus] = useState<'loading' | 'error'>('loading')
  const [step, setStep] = useState<CallbackStep>('loading')
  const [errorDetail, setErrorDetail] = useState<string | null>(null)

  useEffect(() => {
    // Read from the actual URL; useSearchParams() can be empty on first run (hydration)
    const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '')
    const code = params.get('code')
    const state = params.get('state')

    if (!code || !state) {
      queueMicrotask(() => {
        setErrorDetail(t('callbackErrorMissingParams'))
        setStep('error')
        setStatus('error')
      })
      return
    }
    queueMicrotask(() => setStep('have_code_state'))

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
      clearPkceStorage()

      try {
        setStep('exchanging')
        const stateCode = getState()
        const res = await fetch(`/api/auth/oidc/${stateCode}/exchange-code`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code,
            code_verifier: stored.code_verifier
          }),
          credentials: 'include'
        })
        if (cancelled) return
        if (!res.ok) {
          const text = await res.text()
          let data: { error?: string; hint?: string } = {}
          try {
            data = JSON.parse(text) as { error?: string; hint?: string }
          } catch {
            // not JSON
          }
          const msg = data.error ?? text.slice(0, 150)
          const hint = data.hint ? ` ${data.hint}` : ''
          const fallback = `Request failed (${res.status})`
          setErrorDetail((msg || fallback) + hint)
          if (!cancelled) {
            setStep('error')
            setStatus('error')
          }
          return
        }
        const data = (await res.json()) as { token?: string }
        if (data.token) {
          login(data.token)
        }
        router.replace('/dashboard')
      } catch (e) {
        const errMsg = e instanceof Error ? e.message : typeof e === 'string' ? e : 'Unknown error'
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
