import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'

/**
 * DC-584: the /api/* proxy must not let URL-encoded path traversal escape /api/ and
 * reach backend-only endpoints. This runs against the full stack, where the API starts
 * in Development — so /health (always mapped) and /swagger (Development only) actually
 * exist behind the proxy, and a working exploit would return them. The guard must
 * answer 400 instead. State-agnostic; runs in the DC integration job.
 */
test.describe('API proxy path-traversal guard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
  })

  // Encoded slashes keep the traversal inside a single catch-all segment, so it slips
  // past a literal ".." check and resolves to /health or /swagger once decoded.
  const encodedTraversalPaths = [
    '/api/auth%2F..%2F..%2Fhealth',
    '/api/x%2F..%2F..%2Fhealth',
    '/api/features%2F..%2F..%2Fhealth',
    '/api/auth%2F..%2F..%2Fswagger%2Findex.html',
    '/api/auth%2F..%2F..%2Fswagger%2Fv1%2Fswagger.json',
    '/api/auth%2F%2e%2e%2F%2e%2e%2Fhealth'
  ]

  for (const encodedPath of encodedTraversalPaths) {
    test(`rejects encoded traversal ${encodedPath}`, async ({ request }) => {
      const response = await request.get(encodedPath, { failOnStatusCode: false })

      // The proxy's own rejection body — proof the request was blocked here, not
      // proxied to the backend's /health or /swagger.
      expect(response.status()).toBe(400)
      expect(await response.text()).toContain('Invalid path')
    })
  }

  test('still proxies a legitimate /api route to the backend', async ({ request }) => {
    const response = await request.get('/api/features', { failOnStatusCode: false })

    expect(response.status()).toBe(200)
    expect(response.headers()['content-type']).toContain('application/json')
  })

  test('does not expose /health or /swagger directly on the web host', async ({ request }) => {
    const health = await request.get('/health', { failOnStatusCode: false })
    const swagger = await request.get('/swagger/index.html', { failOnStatusCode: false })

    expect(health.status()).toBe(404)
    expect(swagger.status()).toBe(404)
  })
})
