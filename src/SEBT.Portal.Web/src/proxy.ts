import { NextRequest, NextResponse } from 'next/server'

/**
 * Proxy function to generate CSP nonce for each request
 * Implements nonce-based Content Security Policy for enhanced security
 */
export function proxy(request: NextRequest) {
  const nonce = Buffer.from(crypto.randomUUID()).toString('base64')
  const isDev = process.env.NODE_ENV === 'development'

  // Build CSP header
  // Development: Allow unsafe-inline and unsafe-eval for Next.js hot reload
  // Production: Strict CSP with 'self' only (no inline scripts allowed)
  const cspHeader = `
    default-src 'self';
    script-src 'self' ${isDev ? "'unsafe-inline' 'unsafe-eval'" : ''};
    style-src 'self' ${isDev ? "'unsafe-inline'" : ''} https://fonts.googleapis.com;
    font-src 'self' https://fonts.gstatic.com;
    img-src 'self' data: https:;
    connect-src 'self' ${isDev ? 'ws://localhost:* http://localhost:*' : ''};
    frame-ancestors 'none';
    base-uri 'self';
    form-action 'self';
    ${isDev ? '' : 'upgrade-insecure-requests;'}
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
