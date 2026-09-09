import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { getSchools } from './getSchools'

describe('getSchools', () => {
  it('returns school list', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Adams Elementary', code: 'AES' }])
      )
    )
    const schools = await getSchools('')
    expect(schools[0]?.name).toBe('Adams Elementary')
  })

  it('throws on error', async () => {
    server.use(
      http.get('/api/enrollment/schools', () => new HttpResponse(null, { status: 500 }))
    )
    await expect(getSchools('')).rejects.toThrow()
  })
})
