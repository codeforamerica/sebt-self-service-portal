/**
 * PKCE and OIDC helpers for state IdP sign-in (frontend flow).
 */

const CO_PKCE_STORAGE_KEY = 'oidc_co_pkce'

function randomBase64Url(length: number): string {
  const bytes = new Uint8Array(length)
  // crypto.getRandomValues is required for PKCE security — Math.random is not cryptographically secure.
  // All modern browsers support this (since 2013), and generateCodeChallenge already requires crypto.subtle.
  crypto.getRandomValues(bytes)
  return btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

export function generateCodeVerifier(): string {
  return randomBase64Url(32)
}

export async function generateCodeChallenge(verifier: string): Promise<string> {
  const encoder = new TextEncoder()
  const data = encoder.encode(verifier)
  const digest = await crypto.subtle.digest('SHA-256', data)
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

export function generateState(): string {
  return randomBase64Url(24)
}

export interface OidcConfig {
  authorizationEndpoint: string
  tokenEndpoint: string
  clientId: string
  redirectUri: string
  /** Optional params from config **/
  languageParam?: string | undefined
}

export function buildAuthorizationUrl(
  config: OidcConfig,
  codeChallenge: string,
  state: string
): string {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: config.clientId,
    redirect_uri: config.redirectUri,
    scope: 'openid email profile phone',
    state,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    prompt: 'login',
    max_age: '0'
  })
  if (config.languageParam) {
    params.set('language', config.languageParam)
  }
  return `${config.authorizationEndpoint}?${params.toString()}`
}

export interface StoredPkce {
  state: string
  code_verifier: string
  redirect_uri: string
  token_endpoint: string
  client_id: string
}

export function savePkceForCallback(
  state: string,
  codeVerifier: string,
  config: { redirectUri: string; tokenEndpoint: string; clientId: string }
): void {
  if (typeof window === 'undefined') return
  const payload: StoredPkce = {
    state,
    code_verifier: codeVerifier,
    redirect_uri: config.redirectUri,
    token_endpoint: config.tokenEndpoint,
    client_id: config.clientId
  }
  // PKCE data is stored in sessionStorage only — not localStorage.
  // localStorage would persist across tabs/sessions, which could allow stale PKCE to be accepted.
  sessionStorage.setItem(CO_PKCE_STORAGE_KEY, JSON.stringify(payload))
}

export function getPkceFromStorage(): StoredPkce | null {
  if (typeof window === 'undefined') return null
  const raw = sessionStorage.getItem(CO_PKCE_STORAGE_KEY)
  if (!raw) return null
  try {
    const payload = JSON.parse(raw) as StoredPkce
    if (
      payload?.state &&
      payload?.code_verifier &&
      payload?.redirect_uri &&
      payload?.token_endpoint &&
      payload?.client_id
    )
      return payload
  } catch {
    // ignore
  }
  return null
}

export function clearPkceStorage(): void {
  if (typeof window === 'undefined') return
  sessionStorage.removeItem(CO_PKCE_STORAGE_KEY)
}
