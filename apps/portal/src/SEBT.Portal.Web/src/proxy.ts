import { NextRequest, NextResponse } from 'next/server'

/**
 * Proxy function to generate CSP nonce for each request
 * Implements nonce-based Content Security Policy for enhanced security
 *
 * Next.js 16 uses proxy() instead of middleware() for request interception.
 * The nonce is passed via x-nonce header and read in layout.tsx using headers().
 *
 * @see https://nextjs.org/docs/app/guides/content-security-policy
 */
export function proxy(request: NextRequest) {
  // CORS for the enrollment checker static site. Read at runtime so it works
  // in standalone Docker containers where ENROLLMENT_CHECKER_ORIGIN is set as
  // a container env var (not available at build time).
  const enrollmentCheckerOrigin = process.env.ENROLLMENT_CHECKER_ORIGIN
  if (enrollmentCheckerOrigin && /^\/api\/enrollment(\/|$)/.test(request.nextUrl.pathname)) {
    // Preflight: return 204 with CORS headers (don't proxy to the .NET backend,
    // which would return 405 since the controller only defines HttpPost)
    if (request.method === 'OPTIONS') {
      return new NextResponse(null, {
        status: 204,
        headers: {
          'Access-Control-Allow-Origin': enrollmentCheckerOrigin,
          'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type'
        }
      })
    }

    // Actual request: continue to the route handler, then add CORS headers
    const response = NextResponse.next()
    response.headers.set('Access-Control-Allow-Origin', enrollmentCheckerOrigin)
    response.headers.set('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
    response.headers.set('Access-Control-Allow-Headers', 'Content-Type')
    return response
  }

  const nonce = Buffer.from(crypto.randomUUID()).toString('base64')
  const isDev = process.env.NODE_ENV === 'development'
  const hasAmplitude = !!process.env.NEXT_PUBLIC_AMPLITUDE_API_KEY
  const hasMixpanel = !!process.env.NEXT_PUBLIC_MIXPANEL_TOKEN
  const hasSiteImprove = !!process.env.NEXT_PUBLIC_SITEIMPROVE_ID
  const proto =
    request.headers.get('x-forwarded-proto') ?? request.nextUrl.protocol.replace(':', '')
  const isHttps = proto === 'https'

  // Build CSP header with nonce for scripts; 'unsafe-inline' for styles.
  // Development: Allow unsafe-eval for Next.js hot reload.
  // Production: Strict nonce-based policy with strict-dynamic for script loading.
  //
  // Script policy is strict (nonce + strict-dynamic). We intentionally use
  // 'unsafe-inline' for style-src because Socure's Doc Verify Web SDK relies
  // on styled-components, which injects <style> tags at runtime that can't
  // carry our per-request nonce. A nonce-based style policy silently ignores
  // 'unsafe-inline', so there is no way to keep both working together. Styles
  // are a meaningfully lower-value injection target than scripts, and our
  // other defenses (frame-ancestors 'none', no dangerouslySetInnerHTML,
  // React's default escaping) remain in place.
  //
  // 'https://browser-intake-datadoghq.com' in connect-src is Socure's embedded
  // Datadog RUM telemetry endpoint — required by the Doc Verify SDK.
  //
  // Convention: a directive's sources are an array (joined via the helper below)
  // only when any entry is conditional. Fully-static directives stay as plain
  // strings. Add a row to the relevant array when allow-listing a new vendor.
  const join = (parts: (string | false)[]) => parts.filter(Boolean).join(' ')

  const scriptSrc = join([
    "'self'",
    `'nonce-${nonce}'`,
    "'strict-dynamic'",
    'https://www.googletagmanager.com',
    'https://sdk.dv.socure.io',
    hasSiteImprove && 'https://siteimproveanalytics.com',
    hasMixpanel && 'https://cdn.mxpnl.com',
    isDev && "'unsafe-eval'"
  ])

  const styleSrc =
    "'self' 'unsafe-inline' https://fonts.googleapis.com https://verify-v2.socure.com"
  const fontSrc = "'self' https://fonts.gstatic.com https://verify-v2.socure.com"
  const imgSrc = "'self' data: https: https://www.google-analytics.com"

  const oidcIssuerOrigin = process.env.OIDC_ISSUER_ORIGIN

  const connectSrc = join([
    "'self'",
    'https://www.google-analytics.com',
    'https://*.google-analytics.com',
    'https://www.googletagmanager.com',
    'https://auth.pingone.com',
    !!oidcIssuerOrigin && oidcIssuerOrigin,
    'https://*.socure.com',
    'https://*.socure.io',
    'https://browser-intake-datadoghq.com',
    'https://us-autocomplete-pro.api.smarty.com',
    hasAmplitude && 'https://api2.amplitude.com https://sr-client-cfg.amplitude.com',
    hasMixpanel && 'https://api-js.mixpanel.com',
    hasSiteImprove && 'https://siteimproveanalytics.io',
    isDev && 'ws://localhost:* http://localhost:*'
  ])

  const formAction = join(["'self'", !!oidcIssuerOrigin && oidcIssuerOrigin])

  const directives = [
    `default-src 'self'`,
    `script-src ${scriptSrc}`,
    `style-src ${styleSrc}`,
    `font-src ${fontSrc}`,
    `img-src ${imgSrc}`,
    `connect-src ${connectSrc}`,
    `frame-src https://verify-v2.socure.com`,
    `child-src https://verify-v2.socure.com`,
    `worker-src 'self'`,
    `frame-ancestors 'none'`,
    `base-uri 'self'`,
    `form-action ${formAction}`,
    `object-src 'none'`,
    !isDev && isHttps && 'upgrade-insecure-requests'
  ].filter(Boolean)

  const contentSecurityPolicyHeaderValue = directives.join('; ')

  // Set nonce in request headers for use in components
  const requestHeaders = new Headers(request.headers)
  requestHeaders.set('x-nonce', nonce)
  requestHeaders.set('Content-Security-Policy', contentSecurityPolicyHeaderValue)

  const response = NextResponse.next({
    request: {
      headers: requestHeaders
    }
  })

  // Set CSP and other security headers on response
  response.headers.set('Content-Security-Policy', contentSecurityPolicyHeaderValue)
  response.headers.set('X-Frame-Options', 'DENY')
  response.headers.set('X-Content-Type-Options', 'nosniff')
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin')
  response.headers.set(
    'Permissions-Policy',
    'camera=(self "https://verify-v2.socure.com"), microphone=(), geolocation=()'
  )

  return response
}

// Apply middleware to all routes except static files and API routes
export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     * - public folder files
     */
    {
      source: '/((?!_next/static|_next/image|favicon.ico|uswds/).*)',
      missing: [
        { type: 'header', key: 'next-router-prefetch' },
        { type: 'header', key: 'purpose', value: 'prefetch' }
      ]
    }
  ]
}
