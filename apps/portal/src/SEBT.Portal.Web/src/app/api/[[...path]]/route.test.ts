import { NextRequest } from 'next/server'
import { afterEach, describe, expect, it, vi } from 'vitest'

// t3-env does not apply the schema default for BACKEND_URL under test; stub it so
// the proxy has a valid backend base. The exact value is irrelevant to these tests.
vi.mock('@/env', () => ({ env: { BACKEND_URL: 'http://localhost:5280' } }))

import { GET } from './route'

function context(path: string[]) {
  return { params: Promise.resolve({ path }) }
}

describe('/api/[[...path]] proxy route', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('rejects URL-encoded path traversal with 400 and never proxies', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch')
    const request = new NextRequest('http://localhost:3000/api/auth%2F..%2F..%2Fhealth')

    const response = await GET(request, context(['auth%2F..%2F..%2Fhealth']))

    expect(response.status).toBe(400)
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('rejects literal ".." traversal with 400 and never proxies', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch')
    const request = new NextRequest('http://localhost:3000/api/auth/../../health')

    const response = await GET(request, context(['auth', '..', '..', 'health']))

    expect(response.status).toBe(400)
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('proxies a legitimate path to the backend and forwards the query string', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response('ok', { status: 200 }))
    const request = new NextRequest('http://localhost:3000/api/features?state=dc')

    const response = await GET(request, context(['features']))

    expect(response.status).toBe(200)
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(fetchSpy).toHaveBeenCalledWith(
      'http://localhost:5280/api/features?state=dc',
      expect.objectContaining({ method: 'GET' })
    )
  })
})
