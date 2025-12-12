/**
 * MSW Handlers Test (Co-located)
 *
 * Tests for Mock Service Worker handlers
 * Co-located with the handlers they test for better maintainability
 */
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from './server'

describe('MSW Handlers', () => {
  it('should use default health check mock', async () => {
    const response = await fetch('/api/health')
    const data = await response.json()

    expect(data).toEqual({ status: 'ok' })
  })

  it('should allow runtime handler override', async () => {
    // Override the default handler for this specific test
    server.use(
      http.get('/api/health', () => {
        return HttpResponse.json({ status: 'maintenance' })
      })
    )

    const response = await fetch('/api/health')
    const data = await response.json()

    expect(data).toEqual({ status: 'maintenance' })
  })

  it('should handle API errors gracefully', async () => {
    // Mock an error response
    server.use(
      http.get('/api/health', () => {
        return HttpResponse.json({ error: 'Service unavailable' }, { status: 503 })
      })
    )

    const response = await fetch('/api/health')

    expect(response.status).toBe(503)

    const data = await response.json()
    expect(data).toEqual({ error: 'Service unavailable' })
  })
})
