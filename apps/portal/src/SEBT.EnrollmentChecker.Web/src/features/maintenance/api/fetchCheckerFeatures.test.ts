import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { fetchCheckerFeatures } from './fetchCheckerFeatures'

const FEATURES = { maintenanceBanner: { enabled: true, message: { en: 'maintenance soon' } } }

describe('fetchCheckerFeatures', () => {
  it('returns parsed features on success', async () => {
    server.use(
      http.get('/api/enrollment/features', () => HttpResponse.json(FEATURES))
    )
    const features = await fetchCheckerFeatures('')
    expect(features.maintenanceBanner.enabled).toBe(true)
  })

  it('parses the outage page state when the API sends it', async () => {
    server.use(
      http.get('/api/enrollment/features', () =>
        HttpResponse.json({ ...FEATURES, outagePage: { enabled: true } })
      )
    )
    const features = await fetchCheckerFeatures('')
    expect(features.outagePage?.enabled).toBe(true)
  })

  it('tolerates a response without outagePage so an older API cannot break the banner', async () => {
    server.use(
      http.get('/api/enrollment/features', () => HttpResponse.json(FEATURES))
    )
    const features = await fetchCheckerFeatures('')
    expect(features.outagePage).toBeUndefined()
    expect(features.maintenanceBanner.enabled).toBe(true)
  })

  it('includes the resolved URL in the error so console captures distinguish proxy from API failures', async () => {
    server.use(
      http.get('/api/enrollment/features', () => new HttpResponse(null, { status: 500 }))
    )
    await expect(fetchCheckerFeatures('')).rejects.toThrow('/api/enrollment/features')
  })

  it("aborts the request when the caller's signal aborts", async () => {
    server.use(
      http.get('/api/enrollment/features', async () => {
        await delay(5_000)
        return HttpResponse.json(FEATURES)
      })
    )
    const controller = new AbortController()
    const promise = fetchCheckerFeatures('', controller.signal)
    controller.abort()
    await expect(promise).rejects.toThrow()
  })
})
