/**
 * API Client Unit Tests
 *
 * Tests the apiFetch function including:
 * - Successful requests
 * - Error handling (400, 401, 500)
 * - Timeout handling
 * - Schema validation
 */
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { z } from 'zod'

import { server } from '@/mocks/server'

import { ApiError, apiFetch, ApiTimeoutError, ApiValidationError } from './client'

describe('apiFetch', () => {
  describe('Successful Requests', () => {
    it('should return JSON data for successful requests', async () => {
      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ message: 'success', value: 42 })
        })
      )

      const result = await apiFetch<{ message: string; value: number }>('/test')

      expect(result).toEqual({ message: 'success', value: 42 })
    })

    it('should return undefined for 204 No Content responses', async () => {
      server.use(
        http.post('/api/test', () => {
          return new HttpResponse(null, { status: 204 })
        })
      )

      const result = await apiFetch<void>('/test', { method: 'POST' })

      expect(result).toBeUndefined()
    })

    it('should return undefined for 201 Created responses', async () => {
      server.use(
        http.post('/api/test', () => {
          return new HttpResponse(null, { status: 201 })
        })
      )

      const result = await apiFetch<void>('/test', { method: 'POST' })

      expect(result).toBeUndefined()
    })

    it('should send JSON body with POST requests', async () => {
      let receivedBody: unknown

      server.use(
        http.post('/api/test', async ({ request }) => {
          receivedBody = await request.json()
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test', {
        method: 'POST',
        body: { email: 'test@example.com' }
      })

      expect(receivedBody).toEqual({ email: 'test@example.com' })
    })

    it('should include custom headers', async () => {
      let receivedHeaders: Headers | undefined

      server.use(
        http.get('/api/test', ({ request }) => {
          receivedHeaders = request.headers
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test', {
        headers: { 'X-Custom-Header': 'custom-value' }
      })

      expect(receivedHeaders?.get('X-Custom-Header')).toBe('custom-value')
      expect(receivedHeaders?.get('Content-Type')).toBe('application/json')
    })
  })

  describe('Error Handling', () => {
    it('should throw ApiError for 400 Bad Request', async () => {
      server.use(
        http.post('/api/test', () => {
          return HttpResponse.json({ error: 'Invalid input' }, { status: 400 })
        })
      )

      try {
        await apiFetch('/test', { method: 'POST' })
        expect.fail('Expected ApiError to be thrown')
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError)
        expect((error as ApiError).status).toBe(400)
        expect((error as ApiError).message).toBe('Invalid input')
      }
    })

    it('should throw ApiError for 401 Unauthorized', async () => {
      // 401 also triggers window.location.replace; this test covers the throw shape.
      // The redirect side-effect has its own coverage in '401 Redirect Behavior' below.
      const originalLocation = window.location
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { ...originalLocation, replace: vi.fn() }
      })
      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ message: 'Unauthorized' }, { status: 401 })
        })
      )

      try {
        await apiFetch('/test')
        expect.fail('Expected ApiError to be thrown')
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError)
        expect((error as ApiError).status).toBe(401)
        expect((error as ApiError).message).toBe('Unauthorized')
      } finally {
        Object.defineProperty(window, 'location', {
          configurable: true,
          value: originalLocation
        })
      }
    })

    it('should throw ApiError for 500 Internal Server Error', async () => {
      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ error: 'Server error' }, { status: 500 })
        })
      )

      try {
        await apiFetch('/test')
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError)
        expect((error as ApiError).status).toBe(500)
      }
    })

    it('should use default error message when response has no body', async () => {
      server.use(
        http.get('/api/test', () => {
          return new HttpResponse(null, { status: 404 })
        })
      )

      try {
        await apiFetch('/test')
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError)
        expect((error as ApiError).message).toBe('Request failed with status 404')
      }
    })

    it('should include error data in ApiError', async () => {
      server.use(
        http.post('/api/test', () => {
          return HttpResponse.json(
            { error: 'Validation failed', message: 'Email is required' },
            { status: 400 }
          )
        })
      )

      try {
        await apiFetch('/test', { method: 'POST' })
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError)
        expect((error as ApiError).data).toEqual({
          error: 'Validation failed',
          message: 'Email is required'
        })
      }
    })
  })

  describe('Timeout Handling', () => {
    it('should throw ApiTimeoutError when request times out', async () => {
      server.use(
        http.get('/api/test', async () => {
          // Delay longer than timeout
          await new Promise((resolve) => setTimeout(resolve, 200))
          return HttpResponse.json({ success: true })
        })
      )

      await expect(apiFetch('/test', { timeout: 50 })).rejects.toThrow(ApiTimeoutError)
    })

    it('should include endpoint and timeout in error message', async () => {
      server.use(
        http.get('/api/test', async () => {
          await new Promise((resolve) => setTimeout(resolve, 200))
          return HttpResponse.json({ success: true })
        })
      )

      try {
        await apiFetch('/test', { timeout: 50 })
      } catch (error) {
        expect(error).toBeInstanceOf(ApiTimeoutError)
        expect((error as ApiTimeoutError).message).toContain('/test')
        expect((error as ApiTimeoutError).message).toContain('50ms')
      }
    })
  })

  describe('Schema Validation', () => {
    it('should validate response against schema when provided', async () => {
      const schema = z.object({
        id: z.number(),
        name: z.string()
      })

      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ id: 1, name: 'Test' })
        })
      )

      const result = await apiFetch('/test', { schema })

      expect(result).toEqual({ id: 1, name: 'Test' })
    })

    it('should throw ApiValidationError when response does not match schema', async () => {
      const schema = z.object({
        id: z.number(),
        name: z.string()
      })

      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ id: 'not-a-number', name: 123 })
        })
      )

      await expect(apiFetch('/test', { schema })).rejects.toThrow(ApiValidationError)
    })

    it('should include validation errors in ApiValidationError', async () => {
      const schema = z.object({
        id: z.number(),
        name: z.string()
      })

      server.use(
        http.get('/api/test', () => {
          return HttpResponse.json({ id: 'invalid' })
        })
      )

      try {
        await apiFetch('/test', { schema })
      } catch (error) {
        expect(error).toBeInstanceOf(ApiValidationError)
        expect((error as ApiValidationError).message).toContain('/test')
        expect((error as ApiValidationError).errors).toBeDefined()
      }
    })

    it('should skip validation for undefined data', async () => {
      const schema = z.object({ id: z.number() })

      server.use(
        http.post('/api/test', () => {
          return new HttpResponse(null, { status: 204 })
        })
      )

      // Should not throw even though schema is provided
      const result = await apiFetch('/test', { method: 'POST', schema })
      expect(result).toBeUndefined()
    })
  })

  describe('HTTP Methods', () => {
    it('should support GET requests', async () => {
      let requestMethod: string | undefined

      server.use(
        http.get('/api/test', ({ request }) => {
          requestMethod = request.method
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test')
      expect(requestMethod).toBe('GET')
    })

    it('should support POST requests', async () => {
      let requestMethod: string | undefined

      server.use(
        http.post('/api/test', ({ request }) => {
          requestMethod = request.method
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test', { method: 'POST' })
      expect(requestMethod).toBe('POST')
    })

    it('should support PUT requests', async () => {
      let requestMethod: string | undefined

      server.use(
        http.put('/api/test', ({ request }) => {
          requestMethod = request.method
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test', { method: 'PUT' })
      expect(requestMethod).toBe('PUT')
    })

    it('should support DELETE requests', async () => {
      let requestMethod: string | undefined

      server.use(
        http.delete('/api/test', ({ request }) => {
          requestMethod = request.method
          return HttpResponse.json({ success: true })
        })
      )

      await apiFetch('/test', { method: 'DELETE' })
      expect(requestMethod).toBe('DELETE')
    })
  })

  describe('401 Redirect Behavior', () => {
    // Stub window.location.replace so we can verify which endpoints trigger the
    // session-invalid redirect to /login.
    let replaceSpy: ReturnType<typeof vi.fn>
    const originalLocation = window.location

    beforeEach(() => {
      replaceSpy = vi.fn()
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { ...originalLocation, replace: replaceSpy }
      })
    })

    afterEach(() => {
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation
      })
    })

    it('redirects to /login on 401 from a resource endpoint and marks the error as redirecting', async () => {
      // Bug fix: bearer middleware rejects an aged/missing-auth_time token with 401
      // on /household/data. The error is thrown so consumers can decide what to do,
      // but the `isRedirecting` flag tells them the page is navigating away — they
      // should treat it as a loading state and not flash an error UI.
      server.use(
        http.get('/api/household/data', () =>
          HttpResponse.json({ message: 'Unauthorized' }, { status: 401 })
        )
      )

      let caught: unknown
      try {
        await apiFetch('/household/data')
      } catch (err) {
        caught = err
      }

      expect(replaceSpy).toHaveBeenCalledWith('/login')
      expect(caught).toBeInstanceOf(ApiError)
      expect((caught as ApiError).status).toBe(401)
      expect((caught as ApiError).isRedirecting).toBe(true)
    })

    it('redirects to /login on 401 from /auth/refresh', async () => {
      server.use(
        http.post('/api/auth/refresh', () =>
          HttpResponse.json({ message: 'Unauthorized' }, { status: 401 })
        )
      )

      let caught: unknown
      try {
        await apiFetch('/auth/refresh', { method: 'POST' })
      } catch (err) {
        caught = err
      }

      expect(replaceSpy).toHaveBeenCalledWith('/login')
      expect((caught as ApiError).isRedirecting).toBe(true)
    })

    it('does NOT redirect on 401 from /auth/status (bootstrap probe)', async () => {
      // AuthContext uses /auth/status on mount to learn whether the user is logged in;
      // a 401 here means "no session yet" and must not navigate the page.
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({ message: 'Unauthorized' }, { status: 401 })
        )
      )

      let caught: unknown
      try {
        await apiFetch('/auth/status')
      } catch (err) {
        caught = err
      }

      expect(replaceSpy).not.toHaveBeenCalled()
      expect(caught).toBeInstanceOf(ApiError)
      expect((caught as ApiError).isRedirecting).toBe(false)
    })

    it('does NOT redirect on non-401 errors', async () => {
      // 403 (e.g., IAL gating) and 500 leave the user on the page so they can see
      // the appropriate inline message.
      server.use(
        http.get('/api/household/data', () =>
          HttpResponse.json({ message: 'Forbidden' }, { status: 403 })
        )
      )

      let caught: unknown
      try {
        await apiFetch('/household/data')
      } catch (err) {
        caught = err
      }

      expect(replaceSpy).not.toHaveBeenCalled()
      expect((caught as ApiError).isRedirecting).toBe(false)
    })
  })
})
