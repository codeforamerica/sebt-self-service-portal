/**
 * OIDC helpers for state IdP sign-in (frontend flow).
 * PKCE generation (code_verifier, code_challenge, state) is now server-side.
 * This module retains authorization-URL building, sessionStorage for flow metadata
 * (state, redirect_uri, isStepUp, returnUrl), and cleanup helpers.
 */

const CO_PKCE_STORAGE_KEY = 'oidc_co_pkce'

/**
 * Discard PKCE blobs older than this so a long-lived tab cannot complete a flow with stale state.
 * Aligned with typical authorization-code lifetime plus a small buffer.
 */
export const PKCE_STORAGE_MAX_AGE_MS = 15 * 60 * 1000

import type { OidcConfigResponse } from '@/features/auth/api/oidc/schema'

/** Subset of OidcConfigResponse needed to build the authorization URL. */
type AuthUrlConfig = Pick<OidcConfigResponse, 'authorizationEndpoint' | 'clientId' | 'redirectUri'>

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
  config: AuthUrlConfig,
  codeChallenge: string,
  state: string,
  language: string = 'en'
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
    max_age: '0',
    language
  })
  return `${config.authorizationEndpoint}?${params.toString()}`
}

/**
 * code_verifier is no longer stored client-side — it lives in the server's
 * pre-auth session. StoredPkce retains state (for the callback POST), redirect_uri,
 * and flow metadata so the callback page can construct its request.
 */
export interface StoredPkce {
  state: string
  redirect_uri: string
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
  config: {
    redirectUri: string
    clientId: string
    isStepUp?: boolean
    returnUrl?: string
  }
): void {
  if (typeof window === 'undefined') return
  const payload: StoredPkce = {
    state,
    redirect_uri: config.redirectUri,
    client_id: config.clientId,
    storedAtMs: Date.now(),
    ...(config.isStepUp !== undefined && { isStepUp: config.isStepUp }),
    ...(config.returnUrl !== undefined &&
      config.returnUrl !== '' && { returnUrl: config.returnUrl })
  }
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
    if (!payload?.state || !payload?.redirect_uri || !payload?.client_id) {
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
