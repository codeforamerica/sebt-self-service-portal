import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import type { Child } from '../context/EnrollmentContext'
import { enrollmentCheckRequestSchema } from '../schemas/enrollmentSchema'
import { checkEnrollment } from './checkEnrollment'

const children: Child[] = [
  { id: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' },
  { id: '2', firstName: 'John', middleName: 'A', lastName: 'Doe', dateOfBirth: '2017-06-01' }
]

describe('checkEnrollment', () => {
  it('sends correct request shape to /api/enrollment/check', async () => {
    let captured: unknown
    server.use(
      http.post('/api/enrollment/check', async ({ request }) => {
        captured = await request.json()
        return HttpResponse.json({
          results: [
            { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' },
            { checkId: '2', firstName: 'John', lastName: 'Doe', dateOfBirth: '2017-06-01', status: 'NonMatch' }
          ]
        })
      })
    )

    await checkEnrollment(children, '')
    const body = enrollmentCheckRequestSchema.parse(captured)
    expect(body.children).toHaveLength(2)
    expect(body.children[0]?.firstName).toBe('Jane')
    // middleName sent via additionalFields
    expect(body.children[1]?.additionalFields?.['MiddleName']).toBe('A')
  })

  it('returns parsed results', async () => {
    server.use(
      http.post('/api/enrollment/check', () =>
        HttpResponse.json({
          results: [
            { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
          ]
        })
      )
    )
    const result = await checkEnrollment([children[0]!], '')
    expect(result.results[0]?.status).toBe('Match')
  })

  it('throws on 429 rate limit', async () => {
    server.use(
      http.post('/api/enrollment/check', () => new HttpResponse(null, { status: 429 }))
    )
    await expect(checkEnrollment(children, '')).rejects.toThrow('rate')
  })

  it('throws on 503 backend unavailable', async () => {
    server.use(
      http.post('/api/enrollment/check', () => new HttpResponse(null, { status: 503 }))
    )
    await expect(checkEnrollment(children, '')).rejects.toThrow()
  })

  it('uses apiBaseUrl prefix for SSG mode', async () => {
    let url = ''
    server.use(
      http.post('http://portal.example.gov/api/enrollment/check', ({ request }) => {
        url = request.url
        return HttpResponse.json({ results: [] })
      })
    )
    await checkEnrollment(children, 'http://portal.example.gov')
    expect(url).toContain('portal.example.gov')
  })
})
