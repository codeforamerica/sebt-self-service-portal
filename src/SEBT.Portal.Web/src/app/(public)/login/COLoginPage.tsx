'use client'

import { apiFetch } from '@/api'
import { OidcConfigResponseSchema, type OidcConfigResponse } from '@/features/auth'
import {
  buildAuthorizationUrl,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  savePkceForCallback
} from '@/lib/oidc-pkce'
import { getTranslations } from '@/lib/translations'
import type { StateCode } from '@sebt/design-system'
import { Alert, TextLink, getStateLinks } from '@sebt/design-system'
import { useMutation } from '@tanstack/react-query'

async function fetchOidcConfig(state: StateCode): Promise<OidcConfigResponse> {
  return apiFetch<OidcConfigResponse>(`/auth/oidc/${state}/config`, {
    schema: OidcConfigResponseSchema
  })
}

export function COLoginPage({ state }: { state: StateCode }) {
  const links = getStateLinks(state)
  const t = getTranslations('login')
  const tCommon = getTranslations('common')

  const oidcConfig = useMutation({
    mutationFn: () => fetchOidcConfig(state),
    retry: false
  })

  async function startOidcLogin() {
    try {
      const config = await oidcConfig.mutateAsync()
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
    } catch {
      // Error state is managed by useMutation — no additional handling needed
    }
  }

  const errorMessage = oidcConfig.isError ? t('oidcErrorConfigLoad') : null

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

          {errorMessage && (
            <Alert
              variant="error"
              slim
              className="margin-top-2"
            >
              {errorMessage}
            </Alert>
          )}

          <div className="margin-top-4">
            <button
              type="button"
              onClick={startOidcLogin}
              className="usa-button bg-primary-dark text-white border-primary-dark"
              aria-busy={oidcConfig.isPending}
              disabled={oidcConfig.isPending}
            >
              {tCommon('logIn')}
            </button>
          </div>

          <div className="margin-top-2">
            <button
              type="button"
              onClick={startOidcLogin}
              className="usa-button usa-button--outline border-primary text-primary"
              lang="es"
              aria-busy={oidcConfig.isPending}
              disabled={oidcConfig.isPending}
            >
              {tCommon('logInEsp')}
            </button>
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
