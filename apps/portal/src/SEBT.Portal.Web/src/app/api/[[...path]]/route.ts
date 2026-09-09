import { env } from '@/env'
import { resolveApiProxyUrl } from '@/lib/apiProxyPath'
import { context as otelContext, SpanKind, SpanStatusCode, trace } from '@opentelemetry/api'
import { NextRequest, NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const REQUEST_TIMEOUT_MS = 30000

// Manual span around the backend proxy — the single server-side choke point for
// all API traffic. UndiciInstrumentation already traces the raw fetch and
// propagates trace context to the backend; this span adds proxy-level semantics
// (timeout -> 504, backend unreachable -> 502) as span status and recorded
// exceptions, so those failures are visible in traces.
const tracer = trace.getTracer('sebt-portal-web')

type RouteContext = {
  params: Promise<{ path?: string[] }>
}

// all API paths (including auth/oidc/callback) now proxy to the .NET backend.
// The OIDC token exchange was moved from Next.js to .NET so code_verifier and client
// secret never leave the server; the Next.js OIDC callback route is no longer used.

async function proxyRequest(request: NextRequest, context: RouteContext): Promise<NextResponse> {
  const { path } = await context.params

  // Guard against path traversal — literal or URL-encoded — smuggling the request
  // out of /api/ (e.g. /api/x%2f..%2f..%2fhealth resolving to the backend's /health
  // or /swagger). Returns null when the request must be rejected. See @/lib/apiProxyPath.
  const url = resolveApiProxyUrl(path, BACKEND_URL, request.nextUrl.search)
  if (url === null) {
    return NextResponse.json({ error: 'Invalid path' }, { status: 400 })
  }

  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS)

  const span = tracer.startSpan('http.proxy', {
    kind: SpanKind.CLIENT,
    attributes: {
      'http.request.method': request.method,
      'url.path': url.pathname
    }
  })

  return otelContext.with(trace.setSpan(otelContext.active(), span), async () => {
    try {
      const headers = new Headers(request.headers)
      // Remove Next.js specific headers
      headers.delete('host')
      headers.delete('connection')

      // Forward the request to the backend
      const response = await fetch(url.toString(), {
        method: request.method,
        headers,
        body: request.body,
        signal: controller.signal,
        // Pass backend redirects (e.g., OIDC authorize 302) through to the browser
        // instead of following them within the proxy.
        redirect: 'manual',
        // @ts-expect-error - duplex is required for streaming request bodies
        duplex: 'half'
      })

      span.setAttribute('http.response.status_code', response.status)
      if (response.status >= 400) {
        span.setStatus({ code: SpanStatusCode.ERROR })
      }

      // Create response with backend headers
      const responseHeaders = new Headers(response.headers)
      // Remove hop-by-hop headers
      responseHeaders.delete('transfer-encoding')
      responseHeaders.delete('connection')

      return new NextResponse(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: responseHeaders
      })
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        span.setStatus({ code: SpanStatusCode.ERROR, message: 'Request timeout' })
        span.recordException(error)
        return NextResponse.json({ error: 'Request timeout' }, { status: 504 })
      }

      // Only log detailed errors in development to avoid exposing sensitive information
      if (process.env.NODE_ENV === 'development') {
        console.error('Proxy error:', error)
      }
      span.setStatus({ code: SpanStatusCode.ERROR, message: 'Backend unavailable' })
      span.recordException(error instanceof Error ? error : new Error(String(error)))
      return NextResponse.json({ error: 'Backend unavailable' }, { status: 502 })
    } finally {
      clearTimeout(timeoutId)
      span.end()
    }
  })
}

export async function GET(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}

export async function POST(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}

export async function PUT(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}

export async function OPTIONS(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context)
}
