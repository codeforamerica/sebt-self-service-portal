import { env } from '@/lib/env'
import { NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const TIMEOUT_MS = 10_000

export async function GET(): Promise<NextResponse> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS)
  try {
    const response = await fetch(`${BACKEND_URL}/api/enrollment/schools`, {
      signal: controller.signal
    })
    if (!response.ok) return NextResponse.json([], { status: response.status })
    const data = await response.text()
    return new NextResponse(data, {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    })
  } catch {
    // Schools endpoint is optional — return empty list as fallback
    return NextResponse.json([])
  } finally {
    clearTimeout(timeoutId)
  }
}
