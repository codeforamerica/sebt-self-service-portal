import { describe, expect, it } from 'vitest'

import { enrollmentCheckerRoutes } from './analytics-routes'

describe('enrollmentCheckerRoutes', () => {
  it('maps every route to a valid PageContext', () => {
    for (const [pathname, ctx] of Object.entries(enrollmentCheckerRoutes)) {
      expect(pathname).toMatch(/^\//)
      expect(ctx.name).toBeTruthy()
      expect(ctx.flow).toBeTruthy()
      expect(ctx.step).toBeTruthy()
    }
  })

  it('contains entries for all known Enrollment Checker routes', () => {
    const expectedPaths = ['/', '/disclaimer', '/check', '/review', '/results', '/closed']

    for (const path of expectedPaths) {
      expect(enrollmentCheckerRoutes).toHaveProperty(path)
    }
  })

  it('uses unique step values within each flow', () => {
    const flowSteps = new Map<string, Set<string>>()

    for (const ctx of Object.values(enrollmentCheckerRoutes)) {
      if (!flowSteps.has(ctx.flow)) {
        flowSteps.set(ctx.flow, new Set())
      }
      const steps = flowSteps.get(ctx.flow)!
      expect(steps.has(ctx.step)).toBe(false)
      steps.add(ctx.step)
    }
  })
})
