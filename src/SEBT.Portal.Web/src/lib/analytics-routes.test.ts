import { describe, expect, it } from 'vitest'

import { portalRoutes } from './analytics-routes'

describe('portalRoutes', () => {
  it('maps every route to a valid PageContext', () => {
    for (const [pathname, ctx] of Object.entries(portalRoutes)) {
      expect(pathname).toMatch(/^\//)
      expect(ctx.name).toBeTruthy()
      expect(ctx.flow).toBeTruthy()
      expect(ctx.step).toBeTruthy()
    }
  })

  it('contains entries for all known Portal routes', () => {
    const expectedPaths = [
      '/login',
      '/login/verify',
      '/login/id-proofing',
      '/login/id-proofing/doc-verify',
      '/login/id-proofing/off-boarding',
      '/callback',
      '/dashboard',
      '/profile/address',
      '/profile/address/replacement-cards',
      '/profile/address/replacement-cards/select',
      '/profile/address/info'
    ]

    for (const path of expectedPaths) {
      expect(portalRoutes).toHaveProperty(path)
    }
  })

  it('uses unique step values within each flow', () => {
    const flowSteps = new Map<string, Set<string>>()

    for (const ctx of Object.values(portalRoutes)) {
      if (!flowSteps.has(ctx.flow)) {
        flowSteps.set(ctx.flow, new Set())
      }
      const steps = flowSteps.get(ctx.flow)!
      expect(steps.has(ctx.step)).toBe(false)
      steps.add(ctx.step)
    }
  })
})
