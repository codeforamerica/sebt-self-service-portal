import { test } from '@playwright/test'

/** True when Playwright should hit a live API (local stack or GitHub integration job). */
export const isFullStackE2E = process.env.E2E_FULL_STACK === '1'

export function skipUnlessFullStack(): void {
  test.skip(!isFullStackE2E, 'Requires E2E_FULL_STACK=1 with API + Mailpit running')
}
