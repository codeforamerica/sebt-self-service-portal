import { env } from '@/lib/env'
import { NextResponse } from 'next/server'

// Unlike the schools route, this must NOT be force-static: the whole point of the
// endpoint is runtime toggling, so freezing the response at build time would break it.
// (Like its siblings, this file is removed before the static export build.)

const BACKEND_URL = env.BACKEND_URL
const TIMEOUT_MS = 10_000

export async function GET(): Promise<NextResponse> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS)
  try {
    const response = await fetch(`${BACKEND_URL}/api/enrollment/features`, {
      signal: controller.signal,
      cache: 'no-store'
    })
    const data = await response.text()
    return new NextResponse(data, {
      status: response.status,
      headers: { 'Content-Type': 'application/json' }
    })
  } catch {
    // Surface the failure; the client fails closed (no banner) on any non-OK response.
    return NextResponse.json({ error: 'features unavailable' }, { status: 502 })
  } finally {
    clearTimeout(timeoutId)
  }
}
