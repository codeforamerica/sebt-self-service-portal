import { env } from '@/lib/env'
import { NextRequest, NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const TIMEOUT_MS = 30_000
const MAX_BODY_BYTES = 50_000

export async function POST(request: NextRequest): Promise<NextResponse> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS)

  try {
    const body = await request.text()
    if (body.length > MAX_BODY_BYTES) {
      return NextResponse.json({ error: 'Request too large' }, { status: 413 })
    }
    const response = await fetch(`${BACKEND_URL}/api/enrollment/check`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // Forward client IP for rate limiting (NextRequest.ip is not available in App Router)
        'X-Forwarded-For': request.headers.get('x-forwarded-for') ?? ''
      },
      body,
      signal: controller.signal
    })

    const data = await response.text()
    return new NextResponse(data, {
      status: response.status,
      headers: { 'Content-Type': 'application/json' }
    })
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      return NextResponse.json({ error: 'Request timeout' }, { status: 504 })
    }
    if (process.env.NODE_ENV === 'development') {
      console.error('Enrollment check proxy error:', error)
    }
    return NextResponse.json({ error: 'Backend unavailable' }, { status: 502 })
  } finally {
    clearTimeout(timeoutId)
  }
}
