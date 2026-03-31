/**
 * PKCE and OIDC helpers for state IdP sign-in (frontend flow).
 */

const CO_PKCE_STORAGE_KEY = 'oidc_co_pkce'

/**
 * Discard PKCE blobs older than this so a long-lived tab cannot complete a flow with stale state.
 * Aligned with typical authorization-code lifetime plus a small buffer.
 */
export const PKCE_STORAGE_MAX_AGE_MS = 15 * 60 * 1000

function randomBase64Url(length: number): string {
  const bytes = new Uint8Array(length)
  // crypto.getRandomValues is required for PKCE security; Math.random is not cryptographically secure.
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

/**
 * OIDC redirect_uri sent to PingOne must match a value registered on the client.
 * Use the current browser origin so dev on :3001 vs :3000 (or 127.0.0.1) matches
 * the callback page URL; config from the API may still say localhost:3000 only.
 */
export function getOidcRedirectUriForCurrentOrigin(): string {
  if (typeof window === 'undefined') {
    throw new Error('getOidcRedirectUriForCurrentOrigin is client-only')
  }
  return `${window.location.origin}/callback`
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
  /** True when this is a step-up flow (IAL1+ verification). */
  isStepUp?: boolean
  /** URL to redirect to after step-up completes. */
  returnUrl?: string
  /** When this blob was written (`Date.now()`), for max-age checks. */
  storedAtMs: number
}

export function savePkceForCallback(
  state: string,
  codeVerifier: string,
  config: {
    redirectUri: string
    tokenEndpoint: string
    clientId: string
    isStepUp?: boolean
    returnUrl?: string
  }
): void {
  if (typeof window === 'undefined') return
  const payload: StoredPkce = {
    state,
    code_verifier: codeVerifier,
    redirect_uri: config.redirectUri,
    token_endpoint: config.tokenEndpoint,
    client_id: config.clientId,
    storedAtMs: Date.now(),
    ...(config.isStepUp !== undefined && { isStepUp: config.isStepUp }),
    ...(config.returnUrl !== undefined &&
      config.returnUrl !== '' && { returnUrl: config.returnUrl })
  }
  // PKCE data is stored in sessionStorage only, not localStorage:
  // localStorage would persist across tabs/sessions and could allow stale PKCE to be accepted.
  sessionStorage.setItem(CO_PKCE_STORAGE_KEY, JSON.stringify(payload))
}

function parseStoredAtMs(value: unknown): number | null {
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) return null
  return value
}

export function getPkceFromStorage(): StoredPkce | null {
  if (typeof window === 'undefined') return null
  const raw = sessionStorage.getItem(CO_PKCE_STORAGE_KEY)
  if (!raw) return null
  try {
    const payload = JSON.parse(raw) as StoredPkce
    if (
      !payload?.state ||
      !payload?.code_verifier ||
      !payload?.redirect_uri ||
      !payload?.token_endpoint ||
      !payload?.client_id
    ) {
      sessionStorage.removeItem(CO_PKCE_STORAGE_KEY)
      return null
    }
    const storedAtMs = parseStoredAtMs(payload.storedAtMs)
    if (storedAtMs === null || Date.now() - storedAtMs > PKCE_STORAGE_MAX_AGE_MS) {
      sessionStorage.removeItem(CO_PKCE_STORAGE_KEY)
      return null
    }
    return { ...payload, storedAtMs }
  } catch {
    sessionStorage.removeItem(CO_PKCE_STORAGE_KEY)
  }
  return null
}

export function clearPkceStorage(): void {
  if (typeof window === 'undefined') return
  sessionStorage.removeItem(CO_PKCE_STORAGE_KEY)
}
