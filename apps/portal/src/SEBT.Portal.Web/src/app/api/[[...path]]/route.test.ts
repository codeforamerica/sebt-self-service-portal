// @vitest-environment node
import { server } from '@/mocks/server'
import { context as otelContext, SpanStatusCode, trace } from '@opentelemetry/api'
import {
  BasicTracerProvider,
  InMemorySpanExporter,
  SimpleSpanProcessor
} from '@opentelemetry/sdk-trace-base'
import { http, HttpResponse } from 'msw'
import { NextRequest } from 'next/server'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'

// t3-env does not apply the schema default for BACKEND_URL under test; stub it so
// the proxy has a valid backend base. The exact value is irrelevant to these tests.
vi.mock('@/env', () => ({ env: { BACKEND_URL: 'http://localhost:5280' } }))

import { GET, POST } from './route'

// Matches env.BACKEND_URL's default (see src/env.ts) under test.
const BACKEND_URL = 'http://localhost:5280'

function context(path: string[]) {
  return { params: Promise.resolve({ path }) }
}

function makeRequest(method = 'GET', path = '/api/households/me') {
  return new NextRequest(`http://localhost:3000${path}`, { method })
}

function makeContext(segments: string[]) {
  return { params: Promise.resolve({ path: segments }) }
}

let exporter: InMemorySpanExporter

beforeAll(() => {
  // A real (in-memory) tracer provider so route.ts's tracer produces spans we
  // can assert on. The ProxyTracer created at module load delegates here.
  exporter = new InMemorySpanExporter()
  const provider = new BasicTracerProvider({
    spanProcessors: [new SimpleSpanProcessor(exporter)]
  })
  trace.setGlobalTracerProvider(provider)
})

beforeEach(() => {
  exporter.reset()
})

afterEach(() => {
  vi.restoreAllMocks()
})

afterAll(() => {
  trace.disable()
  otelContext.disable()
})

describe('/api/[[...path]] proxy route', () => {
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

describe('proxyRequest OTel instrumentation', () => {
  describe('successful backend response', () => {
    it('records a span named http.proxy with method and path attributes', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () => HttpResponse.json({ ok: true }))
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      const spans = exporter.getFinishedSpans()
      expect(spans).toHaveLength(1)
      expect(spans[0]!.name).toBe('http.proxy')
      expect(spans[0]!.attributes['http.request.method']).toBe('GET')
      expect(spans[0]!.attributes['url.path']).toBe('/api/households/me')
    })

    it('records the response status code on the span', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ ok: true }, { status: 200 })
        )
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      expect(exporter.getFinishedSpans()[0]!.attributes['http.response.status_code']).toBe(200)
    })

    it('does not set span status to ERROR for a 2xx response', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () => HttpResponse.json({ ok: true }))
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      expect(exporter.getFinishedSpans()[0]!.status.code).toBe(SpanStatusCode.UNSET)
    })
  })

  describe('backend error response', () => {
    it('sets span status to ERROR for a 4xx response', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ error: 'Not found' }, { status: 404 })
        )
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      const span = exporter.getFinishedSpans()[0]!
      expect(span.status.code).toBe(SpanStatusCode.ERROR)
      expect(span.attributes['http.response.status_code']).toBe(404)
    })

    it('sets span status to ERROR for a 5xx response', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ error: 'Server error' }, { status: 503 })
        )
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      const span = exporter.getFinishedSpans()[0]!
      expect(span.status.code).toBe(SpanStatusCode.ERROR)
      expect(span.attributes['http.response.status_code']).toBe(503)
    })
  })

  describe('backend unreachable', () => {
    it('sets span status to ERROR and records exception on timeout', async () => {
      const abortError = Object.assign(new Error('The operation was aborted'), {
        name: 'AbortError'
      })
      const originalFetch = globalThis.fetch
      globalThis.fetch = async () => {
        throw abortError
      }

      try {
        const response = await GET(makeRequest('GET'), makeContext(['households', 'me']))
        expect(response.status).toBe(504)
      } finally {
        globalThis.fetch = originalFetch
      }

      const span = exporter.getFinishedSpans()[0]!
      expect(span.status.code).toBe(SpanStatusCode.ERROR)
      expect(span.events.some((e) => e.name === 'exception')).toBe(true)
    })

    it('sets span status to ERROR and records exception when backend is unavailable', async () => {
      const networkError = new Error('Network failure')
      const originalFetch = globalThis.fetch
      globalThis.fetch = async () => {
        throw networkError
      }

      try {
        const response = await GET(makeRequest('GET'), makeContext(['households', 'me']))
        expect(response.status).toBe(502)
      } finally {
        globalThis.fetch = originalFetch
      }

      const span = exporter.getFinishedSpans()[0]!
      expect(span.status.code).toBe(SpanStatusCode.ERROR)
      expect(span.events.some((e) => e.name === 'exception')).toBe(true)
    })
  })

  describe('POST request', () => {
    it('records method as POST on the span', async () => {
      server.use(
        http.post(`${BACKEND_URL}/api/cards/replace`, () => new HttpResponse(null, { status: 204 }))
      )

      await POST(makeRequest('POST', '/api/cards/replace'), makeContext(['cards', 'replace']))

      const span = exporter.getFinishedSpans()[0]!
      expect(span.attributes['http.request.method']).toBe('POST')
      expect(span.attributes['url.path']).toBe('/api/cards/replace')
    })
  })
})
