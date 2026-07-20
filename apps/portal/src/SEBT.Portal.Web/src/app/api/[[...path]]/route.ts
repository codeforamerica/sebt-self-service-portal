import { env } from '@/env'
import { NextRequest, NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const REQUEST_TIMEOUT_MS = 30000

type RouteContext = {
  params: Promise<{ path?: string[] }>
}

// all API paths (including auth/oidc/callback) now proxy to the .NET backend.
// The OIDC token exchange was moved from Next.js to .NET so code_verifier and client
// secret never leave the server; the Next.js OIDC callback route is no longer used.

async function proxyRequest(request: NextRequest, context: RouteContext): Promise<NextResponse> {
  const { path } = await context.params

  // Reject path segments that could escape /api/ (e.g., ".." → /api/../internal/metrics)
  if (path?.some((segment) => segment === '..' || segment === '.')) {
    return NextResponse.json({ error: 'Invalid path' }, { status: 400 })
  }

  const pathname = path ? `/api/${path.join('/')}` : '/api'
  const url = new URL(pathname, BACKEND_URL)
  url.search = request.nextUrl.search

  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS)

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
      return NextResponse.json({ error: 'Request timeout' }, { status: 504 })
    }

    // Only log detailed errors in development to avoid exposing sensitive information
    if (process.env.NODE_ENV === 'development') {
      console.error('Proxy error:', error)
    }
    return NextResponse.json({ error: 'Backend unavailable' }, { status: 502 })
  } finally {
    clearTimeout(timeoutId)
  }
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
