/**
 * OIDC callback: exchange code + code_verifier with the IdP, validate id_token, return a short-lived callbackToken for .NET complete-login.
 * Used when OIDC exchange and validation are done in Next.js (client secret and signing key in env); .NET only does session creation and portal JWT.
 */

import { env } from '@/env'
import {
  OidcCallbackRequestSchema,
  OidcDiscoveryResponseSchema,
  OidcTokenResponseSchema
} from '@/features/auth/api/oidc/schema'
import { SignJWT, createRemoteJWKSet, errors as joseErrors, jwtVerify } from 'jose'
import { NextRequest, NextResponse } from 'next/server'

const CALLBACK_TOKEN_EXPIRY_SEC = 300 // 5 minutes
const ID_TOKEN_MAX_AGE = '1 hour'

export async function POST(request: NextRequest) {
  // Parse and validate request body with Zod
  let rawBody: unknown
  try {
    rawBody = await request.json()
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body.' }, { status: 400 })
  }

  const bodyResult = OidcCallbackRequestSchema.safeParse(rawBody)
  if (!bodyResult.success) {
    return NextResponse.json(
      { error: 'Missing or invalid code, code_verifier, or stateCode.' },
      { status: 400 }
    )
  }
  const { code, code_verifier, stateCode } = bodyResult.data

  const currentState = env.NEXT_PUBLIC_STATE
  if (stateCode !== currentState) {
    return NextResponse.json({ error: 'stateCode must match current state.' }, { status: 400 })
  }

  const discoveryEndpoint = env.OIDC_DISCOVERY_ENDPOINT
  const clientId = env.OIDC_CLIENT_ID
  const clientSecret = env.OIDC_CLIENT_SECRET
  const redirectUri = env.OIDC_REDIRECT_URI
  const signingKey = env.OIDC_COMPLETE_LOGIN_SIGNING_KEY

  if (!discoveryEndpoint || !clientId || !clientSecret || !redirectUri || !signingKey) {
    return NextResponse.json(
      {
        error: 'OIDC not configured.',
        hint: 'Set OIDC_DISCOVERY_ENDPOINT, OIDC_CLIENT_ID, OIDC_CLIENT_SECRET, OIDC_REDIRECT_URI, OIDC_COMPLETE_LOGIN_SIGNING_KEY.'
      },
      { status: 503 }
    )
  }

  // Step 1: Fetch OIDC discovery document
  let discovery
  try {
    const discoveryRes = await fetch(discoveryEndpoint)
    if (!discoveryRes.ok) {
      console.error('[OIDC] Discovery endpoint returned', discoveryRes.status)
      return NextResponse.json(
        { error: 'Failed to load OIDC discovery document.' },
        { status: 502 }
      )
    }
    const discoveryJson: unknown = await discoveryRes.json()
    const parsed = OidcDiscoveryResponseSchema.safeParse(discoveryJson)
    if (!parsed.success) {
      console.error('[OIDC] Invalid discovery document:', parsed.error.issues)
      return NextResponse.json(
        { error: 'Invalid discovery document (missing token_endpoint or jwks_uri).' },
        { status: 502 }
      )
    }
    discovery = parsed.data
  } catch (err) {
    console.error('[OIDC] Discovery fetch failed:', err)
    return NextResponse.json({ error: 'Failed to reach OIDC discovery endpoint.' }, { status: 502 })
  }

  // Step 2: Exchange authorization code for tokens
  let idToken: string
  let accessToken: string | undefined
  try {
    const tokenParams = new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      redirect_uri: redirectUri,
      code_verifier: code_verifier
    })
    const tokenRes = await fetch(discovery.token_endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        Authorization: `Basic ${Buffer.from(`${clientId}:${clientSecret}`).toString('base64')}`
      },
      body: tokenParams.toString()
    })
    const tokenJson: unknown = await tokenRes.json()
    if (!tokenRes.ok) {
      const errBody = tokenJson as { error_description?: string; error?: string }
      const msg = errBody?.error_description ?? errBody?.error ?? 'Token exchange failed.'
      console.error('[OIDC] Token exchange failed:', msg)
      return NextResponse.json({ error: msg }, { status: 400 })
    }
    const parsed = OidcTokenResponseSchema.safeParse(tokenJson)
    if (!parsed.success) {
      console.error('[OIDC] Invalid token response:', parsed.error.issues)
      return NextResponse.json({ error: 'No id_token in token response.' }, { status: 400 })
    }
    idToken = parsed.data.id_token
    accessToken = parsed.data.access_token
  } catch (err) {
    console.error('[OIDC] Token exchange request failed:', err)
    return NextResponse.json({ error: 'Token exchange request failed.' }, { status: 502 })
  }

  // Step 3: Verify id_token JWT signature and claims
  let payload: Record<string, unknown>
  try {
    const JWKS = createRemoteJWKSet(new URL(discovery.jwks_uri))
    const verifyOptions = {
      maxTokenAge: ID_TOKEN_MAX_AGE,
      audience: clientId,
      ...(discovery.issuer && { issuer: discovery.issuer })
    }
    const result = await jwtVerify(idToken, JWKS, verifyOptions)
    payload = result.payload as Record<string, unknown>
  } catch (err) {
    if (err instanceof joseErrors.JWTExpired) {
      console.error('[OIDC] id_token expired')
      return NextResponse.json({ error: 'Id token has expired.' }, { status: 400 })
    }
    if (err instanceof joseErrors.JWSSignatureVerificationFailed) {
      console.error('[OIDC] id_token signature verification failed')
      return NextResponse.json(
        { error: 'Id token signature verification failed.' },
        { status: 400 }
      )
    }
    if (err instanceof joseErrors.JWTClaimValidationFailed) {
      console.error('[OIDC] id_token claim validation failed:', (err as Error).message)
      return NextResponse.json({ error: 'Id token validation failed.' }, { status: 400 })
    }
    console.error('[OIDC] id_token verification failed:', err)
    return NextResponse.json({ error: 'Id token validation failed.' }, { status: 400 })
  }

  // Step 4: Extract claims from id_token
  const claims: Record<string, string | number | boolean> = {}
  /* eslint-disable security/detect-object-injection -- keys from Object.entries of a verified JWT payload */
  for (const [k, v] of Object.entries(payload)) {
    if (v !== undefined && v !== null && typeof v !== 'object') {
      claims[k] = v as string | number | boolean
    }
  }
  /* eslint-enable security/detect-object-injection */

  // Resolve sub and email from id_token claims
  if (typeof payload.sub === 'string') claims.sub = payload.sub
  else if (typeof payload.sub === 'number') claims.sub = String(payload.sub)
  else if (typeof payload.userId === 'string') claims.sub = payload.userId

  if (typeof payload.email === 'string') claims.email = payload.email
  else if (typeof payload.preferred_username === 'string') claims.email = payload.preferred_username

  // Step 5: Fetch userinfo to supplement claims (phone, name — often not in id_token)
  if (discovery.userinfo_endpoint && accessToken) {
    try {
      const userinfoRes = await fetch(discovery.userinfo_endpoint, {
        headers: { Authorization: `Bearer ${accessToken}` }
      })
      if (userinfoRes.ok) {
        const userinfo = (await userinfoRes.json()) as Record<string, unknown>
        mergeUserinfoClaims(claims, userinfo)
      } else {
        console.error('[OIDC] Userinfo endpoint returned', userinfoRes.status)
      }
    } catch (err) {
      // Userinfo is supplemental; log but continue with id_token claims
      console.error('[OIDC] Userinfo fetch failed:', err)
    }
  }

  if (typeof claims.email !== 'string' || !claims.email) {
    return NextResponse.json(
      {
        error: 'Callback token must contain an email claim.',
        hint: 'IdP id_token (and optional userinfo) had no email. Request scopes openid email profile and ensure the IdP returns email.'
      },
      { status: 400 }
    )
  }

  // Step 6: Sign claims into a short-lived callback token for .NET complete-login
  const secret = new TextEncoder().encode(signingKey)
  const callbackToken = await new SignJWT(claims)
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuedAt()
    .setExpirationTime(`${CALLBACK_TOKEN_EXPIRY_SEC}s`)
    .sign(secret)

  return NextResponse.json({ callbackToken })
}

/**
 * Merge supplemental claims from the userinfo endpoint into the claims object.
 * IdPs often put phone, given_name, family_name in userinfo rather than id_token.
 */
function mergeUserinfoClaims(
  claims: Record<string, string | number | boolean>,
  userinfo: Record<string, unknown>
): void {
  if (typeof userinfo.sub === 'string' && !claims.sub) claims.sub = userinfo.sub
  if (typeof userinfo.email === 'string' && !claims.email) claims.email = userinfo.email
  if (
    typeof userinfo.preferred_username === 'string' &&
    !claims.email &&
    userinfo.preferred_username.includes('@')
  ) {
    claims.email = userinfo.preferred_username
  }
  if (typeof userinfo.phone === 'string') claims.phone = userinfo.phone
  if (typeof userinfo.phone_number === 'string') claims.phone_number = userinfo.phone_number
  if (typeof userinfo.given_name === 'string') claims.givenName = userinfo.given_name
  if (typeof userinfo.givenName === 'string') claims.givenName = userinfo.givenName as string
  if (typeof userinfo.family_name === 'string') claims.familyName = userinfo.family_name
  if (typeof userinfo.familyName === 'string') claims.familyName = userinfo.familyName as string
  if (typeof userinfo.name === 'string') claims.name = userinfo.name
}
