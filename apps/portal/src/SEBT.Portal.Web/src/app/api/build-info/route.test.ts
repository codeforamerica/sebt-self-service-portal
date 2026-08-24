// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { GET } from './route'

describe('/api/build-info', () => {
  beforeEach(() => {
    vi.unstubAllEnvs()
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('returns the build SHA and DC connector SHA baked in at build time', async () => {
    vi.stubEnv('NEXT_PUBLIC_BUILD_SHA', 'b033fef68acbf76fa6da4f3202ee82f3305774af')
    vi.stubEnv('NEXT_PUBLIC_DC_CONNECTOR_SHA', 'abc1234')

    const response = await GET()

    expect(response.status).toBe(200)
    await expect(response.json()).resolves.toEqual({
      buildSha: 'b033fef68acbf76fa6da4f3202ee82f3305774af',
      dcConnectorSha: 'abc1234'
    })
  })

  it('returns null fields when neither build-time value is set (e.g. local dev)', async () => {
    const response = await GET()

    await expect(response.json()).resolves.toEqual({
      buildSha: null,
      dcConnectorSha: null
    })
  })

  it('returns null dcConnectorSha for CO builds, which never set it', async () => {
    vi.stubEnv('NEXT_PUBLIC_BUILD_SHA', '5155fc35')

    const response = await GET()

    await expect(response.json()).resolves.toEqual({
      buildSha: '5155fc35',
      dcConnectorSha: null
    })
  })
})
