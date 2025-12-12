/**
 * MSW Request Handlers
 *
 * Define mock API responses for testing.
 * Add your API routes here as you build features.
 *
 * Example:
 * export const handlers = [
 *   http.get('/api/applications/:id', () => {
 *     return HttpResponse.json({ id: '1', status: 'pending' })
 *   }),
 * ]
 */
import { http, HttpResponse } from 'msw'

export const handlers = [
  // Example: Mock health check endpoint
  http.get('/api/health', () => {
    return HttpResponse.json({ status: 'ok' })
  })

  // Add your API mocks here as you develop features
]
