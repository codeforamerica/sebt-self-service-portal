'use client'

import { useAuth } from '@/features/auth'
import { clearPkceStorage, getPkceFromStorage } from '@/lib/oidc-pkce'
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
        setErrorDetail('Missing code or state in URL')
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
        setErrorDetail('No PKCE data (same-tab flow required)')
        clearPkceStorage()
        if (!cancelled) {
          setStep('error')
          setStatus('error')
        }
        return
      }
      if (stored.state !== state) {
        setErrorDetail('State mismatch')
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
        const res = await fetch('/api/auth/oidc/co/exchange-code', {
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
        setErrorDetail(errMsg || 'Something went wrong')
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
  }, [login, router])

  useEffect(() => {
    if (status === 'error') {
      // Give user a moment to read the error before redirecting to login
      const t = setTimeout(() => router.replace('/login'), 5000)
      return () => clearTimeout(t)
    }
    return undefined
  }, [status, router])

  const stepMessage: Record<CallbackStep, string> = {
    loading: 'Loading…',
    have_code_state: 'Code and state found, checking PKCE…',
    have_pkce: 'PKCE found, sending code to backend…',
    exchanging: 'Exchanging code with sign-in provider…',
    error: errorDetail ?? 'Something went wrong.'
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <p className="font-sans-md">
          {status === 'error' ? (
            <>
              <strong>Sign-in issue:</strong> {errorDetail}
            </>
          ) : (
            // eslint-disable-next-line security/detect-object-injection -- step is CallbackStep union
            <>Signing you in… ({stepMessage[step]})</>
          )}
        </p>
      </div>
    </div>
  )
}
