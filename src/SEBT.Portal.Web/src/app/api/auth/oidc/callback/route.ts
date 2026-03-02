/**
 * OIDC callback: exchange code + code_verifier with the IdP, validate id_token, return a short-lived callbackToken for .NET complete-login.
 * Used when OIDC exchange and validation are done in Next.js (client secret and signing key in env); .NET only does session creation and portal JWT.
 */

import { env } from '@/env'
import { SignJWT, createRemoteJWKSet, jwtVerify } from 'jose'
import { NextRequest, NextResponse } from 'next/server'

const CALLBACK_TOKEN_EXPIRY_SEC = 300 // 5 minutes

function getCurrentStateCode(): string {
  return (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase()
}

export async function POST(request: NextRequest) {
  let body: { code?: string; code_verifier?: string; state?: string; stateCode?: string }
  try {
    body = (await request.json()) as typeof body
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body.' }, { status: 400 })
  }

  const currentState = getCurrentStateCode()
  const { code, code_verifier, stateCode } = body
  if (!code || !code_verifier || stateCode !== currentState) {
    return NextResponse.json(
      { error: 'Missing or invalid code, code_verifier, or stateCode (must match current state).' },
      { status: 400 }
    )
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

  try {
    const discoveryRes = await fetch(discoveryEndpoint)
    if (!discoveryRes.ok) {
      return NextResponse.json(
        { error: 'Failed to load OIDC discovery document.' },
        { status: 502 }
      )
    }
    const discovery = (await discoveryRes.json()) as {
      token_endpoint?: string
      jwks_uri?: string
      issuer?: string
    }
    const tokenEndpoint = discovery.token_endpoint
    const jwksUri = discovery.jwks_uri
    if (!tokenEndpoint || !jwksUri) {
      return NextResponse.json(
        { error: 'Invalid discovery document (missing token_endpoint or jwks_uri).' },
        { status: 502 }
      )
    }

    const tokenParams = new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      redirect_uri: redirectUri,
      code_verifier: code_verifier
    })
    const tokenRes = await fetch(tokenEndpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        Authorization: `Basic ${Buffer.from(`${clientId}:${clientSecret}`).toString('base64')}`
      },
      body: tokenParams.toString()
    })
    const tokenJson = (await tokenRes.json()) as {
      error?: string
      error_description?: string
      id_token?: string
    }
    if (!tokenRes.ok) {
      const msg = tokenJson.error_description ?? tokenJson.error ?? 'Token exchange failed.'
      return NextResponse.json({ error: msg }, { status: 400 })
    }
    const idToken = tokenJson.id_token
    if (!idToken) {
      return NextResponse.json({ error: 'No id_token in token response.' }, { status: 400 })
    }

    const JWKS = createRemoteJWKSet(new URL(jwksUri))
    const verifyOptions: { maxTokenAge: string; issuer?: string } = { maxTokenAge: '1 hour' }
    if (discovery.issuer) verifyOptions.issuer = discovery.issuer
    const { payload } = await jwtVerify(idToken, JWKS, verifyOptions)

    const claims: Record<string, string | number | boolean> = {}
    for (const [k, v] of Object.entries(payload)) {
      if (v !== undefined && v !== null && typeof v !== 'object') {
        claims[k] = v as string | number | boolean
      } else if (typeof v === 'string') {
        claims[k] = v
      }
    }

    const secret = new TextEncoder().encode(signingKey)
    const callbackToken = await new SignJWT(claims)
      .setProtectedHeader({ alg: 'HS256' })
      .setIssuedAt()
      .setExpirationTime(`${CALLBACK_TOKEN_EXPIRY_SEC}s`)
      .sign(secret)

    return NextResponse.json({ callbackToken })
  } catch (err) {
    if (err instanceof Error) {
      if (err.message?.includes('expired') || err.message?.includes('signature')) {
        return NextResponse.json({ error: 'Id token validation failed.' }, { status: 400 })
      }
    }
    return NextResponse.json({ error: 'OIDC callback failed.' }, { status: 400 })
  }
}
