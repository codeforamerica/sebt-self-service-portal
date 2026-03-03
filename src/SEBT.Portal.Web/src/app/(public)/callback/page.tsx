'use client'

import { setAuthToken, useAuth } from '@/features/auth'
import { clearPkceStorage, getPkceFromStorage } from '@/lib/oidc-pkce'
import { getState } from '@/lib/state'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

type CallbackStep = 'loading' | 'have_code_state' | 'have_pkce' | 'exchanging' | 'error'

/**
 * OIDC callback: state IdP redirects here with ?code=...&state=...
 * We then send callbackToken to the .NET complete-login endpoint to create session and get the portal JWT.
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
        const callbackRes = await fetch('/api/auth/oidc/callback', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code,
            code_verifier: stored.code_verifier,
            state,
            stateCode: getState()
          }),
          credentials: 'include'
        })
        if (cancelled) return
        if (!callbackRes.ok) {
          const text = await callbackRes.text()
          let data: { error?: string; hint?: string } = {}
          try {
            data = JSON.parse(text) as { error?: string; hint?: string }
          } catch {
            // not JSON
          }
          const isHtml =
            text.trimStart().startsWith('<!') ||
            (callbackRes.headers.get('content-type') ?? '').toLowerCase().includes('text/html')
          const msg =
            data.error ??
            (isHtml
              ? `Sign-in provider returned an error page (${callbackRes.status}). Try again or check configuration.`
              : text.slice(0, 150))
          const hint = data.hint ? ` ${data.hint}` : ''
          setErrorDetail((msg || `Request failed (${callbackRes.status})`) + hint)
          if (!cancelled) {
            setStep('error')
            setStatus('error')
          }
          return
        }
        const { callbackToken } = (await callbackRes.json()) as { callbackToken?: string }
        if (!callbackToken) {
          setErrorDetail('No callback token returned')
          if (!cancelled) {
            setStep('error')
            setStatus('error')
          }
          return
        }
        const completeRes = await fetch('/api/auth/oidc/complete-login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ stateCode: getState(), callbackToken }),
          credentials: 'include'
        })
        if (cancelled) return
        if (!completeRes.ok) {
          const text = await completeRes.text()
          let data: { error?: string; hint?: string } = {}
          try {
            data = JSON.parse(text) as { error?: string; hint?: string }
          } catch {
            // not JSON
          }
          const isHtml =
            text.trimStart().startsWith('<!') ||
            (completeRes.headers.get('content-type') ?? '').toLowerCase().includes('text/html')
          const msg =
            data.error ??
            (isHtml
              ? `Server returned an error page (${completeRes.status}). Check that the API is running and reachable.`
              : text.slice(0, 150))
          const hint = data.hint ? ` ${data.hint}` : ''
          setErrorDetail((msg || `Complete login failed (${completeRes.status})`) + hint)
          if (!cancelled) {
            setStep('error')
            setStatus('error')
          }
          return
        }
        const data = (await completeRes.json()) as { token?: string }
        if (data.token) {
          // Persist to sessionStorage and notify auth context (setAuthToken ensures storage even if login context is stale)
          setAuthToken(data.token)
          login(data.token)
          // Ensure token is stored and listeners have run before navigating (avoids 401 on refresh when dashboard loads)
          await new Promise((resolve) => setTimeout(resolve, 0))
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
