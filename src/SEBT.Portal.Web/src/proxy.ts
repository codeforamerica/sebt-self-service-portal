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
  const nonce = Buffer.from(crypto.randomUUID()).toString('base64')
  const isDev = process.env.NODE_ENV === 'development'
  const proto =
    request.headers.get('x-forwarded-proto') ?? request.nextUrl.protocol.replace(':', '')
  const isHttps = proto === 'https'
  // Only add upgrade-insecure-requests when actually served over HTTPS.
  const upgradeInsecure = !isDev && isHttps ? 'upgrade-insecure-requests;' : ''

  // Build CSP header with nonce for script and style sources
  // Development: Allow unsafe-eval for Next.js hot reload, unsafe-inline for styles (no nonce for styles)
  // Production: Strict nonce-based policy with strict-dynamic for script loading
  //
  // Note: When nonce is present, 'unsafe-inline' is ignored by browsers.
  // In dev, we skip style nonce to allow HMR style injection.
  const cspHeader = `
    default-src 'self';
    script-src 'self' 'nonce-${nonce}' 'strict-dynamic' https://www.googletagmanager.com ${isDev ? "'unsafe-eval'" : ''};
    style-src 'self' ${isDev ? "'unsafe-inline'" : `'nonce-${nonce}'`} https://fonts.googleapis.com;
    font-src 'self' https://fonts.gstatic.com;
    img-src 'self' data: https: https://www.google-analytics.com;
    connect-src 'self' https://www.google-analytics.com https://*.google-analytics.com https://www.googletagmanager.com https://auth.pingone.com ${isDev ? 'ws://localhost:* http://localhost:*' : ''};
    frame-src 'none';
    child-src 'none';
    worker-src 'self';
    frame-ancestors 'none';
    base-uri 'self';
    form-action 'self';
    object-src 'none';
    ${upgradeInsecure}
  `

  const contentSecurityPolicyHeaderValue = cspHeader.replace(/\s{2,}/g, ' ').trim()

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
  response.headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=()')

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
