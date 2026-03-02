'use client'

import { TextLink } from '@/components/ui'
import { getStateLinks } from '@/lib/links'
import {
  buildAuthorizationUrl,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  savePkceForCallback,
  type OidcConfig
} from '@/lib/oidc-pkce'
import { getTranslations } from '@/lib/translations'
import Link from 'next/link'
import { useState } from 'react'

import type { StateCode } from '@/lib/state'

export function COLoginPage({ state }: { state: StateCode }) {
  const links = getStateLinks(state)
  const t = getTranslations('login')
  const tCommon = getTranslations('common')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function startOidcLogin(e: React.MouseEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const res = await fetch(`/api/auth/oidc/${state}/config`, { credentials: 'include' })
      if (!res.ok) {
        const data = (await res.json()) as { error?: string }
        setError(data.error ?? 'Unable to load login configuration.')
        return
      }
      const config = (await res.json()) as OidcConfig
      const codeVerifier = generateCodeVerifier()
      const codeChallenge = await generateCodeChallenge(codeVerifier)
      const stateValue = generateState()
      savePkceForCallback(stateValue, codeVerifier, {
        redirectUri: config.redirectUri,
        tokenEndpoint: config.tokenEndpoint,
        clientId: config.clientId
      })
      const authUrl = buildAuthorizationUrl(config, codeChallenge, stateValue)
      window.location.href = authUrl
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="login-title">
          <h1
            id="login-title"
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3 text-primary-dark"
          >
            {t('title')}
          </h1>

          <p className="margin-top-4 font-sans-sm">{t('logInDisclaimerBody1')}</p>

          {error && (
            <p
              className="margin-top-2 font-sans-sm text-red"
              role="alert"
            >
              {error}
            </p>
          )}

          <div className="margin-top-4">
            <Link
              href="#"
              onClick={startOidcLogin}
              className="usa-button bg-primary-dark text-white border-primary-dark"
              aria-busy={loading}
            >
              {tCommon('logIn')}
            </Link>
          </div>

          <div className="margin-top-2">
            <Link
              href="#"
              onClick={startOidcLogin}
              className="usa-button usa-button--outline border-primary text-primary"
              lang="es"
              aria-busy={loading}
            >
              {tCommon('logInEsp')}
            </Link>
          </div>

          <p className="margin-top-4 font-sans-sm">
            <TextLink href={links.external.contactUsAssistance}>
              {t('logInDisclaimerBody2')}
            </TextLink>
          </p>
        </section>
      </div>
    </div>
  )
}
