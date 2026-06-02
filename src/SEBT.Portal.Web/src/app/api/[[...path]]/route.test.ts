// @vitest-environment node
import { BasicTracerProvider, InMemorySpanExporter, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-base'
import { SpanStatusCode, context as otelContext, trace } from '@opentelemetry/api'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { NextRequest } from 'next/server'

import { server } from '@/mocks/server'
import { GET, POST } from './route'

const BACKEND_URL = 'http://localhost:5280'

function makeRequest(method = 'GET', path = '/api/households/me') {
  return new NextRequest(`http://localhost:3000${path}`, { method })
}

function makeContext(segments: string[]) {
  return { params: Promise.resolve({ path: segments }) }
}

let exporter: InMemorySpanExporter

beforeAll(() => {
  exporter = new InMemorySpanExporter()
  const provider = new BasicTracerProvider({
    spanProcessors: [new SimpleSpanProcessor(exporter)],
  })
  trace.setGlobalTracerProvider(provider)
})

beforeEach(() => {
  exporter.reset()
})

afterAll(() => {
  trace.disable()
  otelContext.disable()
})

describe('proxyRequest OTel instrumentation', () => {
  describe('successful backend response', () => {
    it('records a span named http.proxy with method and path attributes', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ ok: true })
        )
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

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.attributes['http.response.status_code']).toBe(200)
    })

    it('does not set span status to ERROR for a 2xx response', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ ok: true })
        )
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.status.code).toBe(SpanStatusCode.UNSET)
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

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.status.code).toBe(SpanStatusCode.ERROR)
      expect(spans[0]!.attributes['http.response.status_code']).toBe(404)
    })

    it('sets span status to ERROR for a 5xx response', async () => {
      server.use(
        http.get(`${BACKEND_URL}/api/households/me`, () =>
          HttpResponse.json({ error: 'Server error' }, { status: 503 })
        )
      )

      await GET(makeRequest('GET'), makeContext(['households', 'me']))

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.status.code).toBe(SpanStatusCode.ERROR)
      expect(spans[0]!.attributes['http.response.status_code']).toBe(503)
    })
  })

  describe('backend unreachable', () => {
    it('sets span status to ERROR and records exception on timeout', async () => {
      const abortError = Object.assign(new Error('The operation was aborted'), {
        name: 'AbortError',
      })
      const originalFetch = globalThis.fetch
      globalThis.fetch = async () => { throw abortError }

      try {
        const response = await GET(makeRequest('GET'), makeContext(['households', 'me']))
        expect(response.status).toBe(504)
      } finally {
        globalThis.fetch = originalFetch
      }

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.status.code).toBe(SpanStatusCode.ERROR)
      expect(spans[0]!.events.some((e) => e.name === 'exception')).toBe(true)
    })

    it('sets span status to ERROR and records exception when backend is unavailable', async () => {
      const networkError = new Error('Network failure')
      const originalFetch = globalThis.fetch
      globalThis.fetch = async () => { throw networkError }

      try {
        const response = await GET(makeRequest('GET'), makeContext(['households', 'me']))
        expect(response.status).toBe(502)
      } finally {
        globalThis.fetch = originalFetch
      }

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.status.code).toBe(SpanStatusCode.ERROR)
      expect(spans[0]!.events.some((e) => e.name === 'exception')).toBe(true)
    })
  })

  describe('POST request', () => {
    it('records method as POST on the span', async () => {
      server.use(
        http.post(`${BACKEND_URL}/api/cards/replace`, () =>
          new HttpResponse(null, { status: 204 })
        )
      )

      await POST(makeRequest('POST', '/api/cards/replace'), makeContext(['cards', 'replace']))

      const spans = exporter.getFinishedSpans()
      expect(spans[0]!.attributes['http.request.method']).toBe('POST')
      expect(spans[0]!.attributes['url.path']).toBe('/api/cards/replace')
    })
  })
})
