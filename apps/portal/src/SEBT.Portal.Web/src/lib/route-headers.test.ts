import { describe, expect, it } from 'vitest'
import { getRouteHeaders } from './route-headers'

describe('getRouteHeaders', () => {
  it('returns a no-store Cache-Control header for /login so the browser does not put it in BFCache', async () => {
    const entries = await getRouteHeaders()
    const loginEntry = entries.find((e) => e.source === '/login')

    expect(loginEntry).toBeDefined()
    expect(loginEntry?.headers).toContainEqual({
      key: 'Cache-Control',
      value: 'no-store'
    })
  })

  it('matches the Next.js Header type shape (source string + headers array)', async () => {
    const entries = await getRouteHeaders()
    for (const entry of entries) {
      expect(typeof entry.source).toBe('string')
      expect(Array.isArray(entry.headers)).toBe(true)
      for (const h of entry.headers) {
        expect(typeof h.key).toBe('string')
        expect(typeof h.value).toBe('string')
      }
    }
  })
})
