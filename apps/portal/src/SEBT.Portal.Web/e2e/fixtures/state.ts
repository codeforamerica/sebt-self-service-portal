import { test } from '@playwright/test'

import type { StateCode } from '@sebt/design-system/src/lib/state'

export type { StateCode }

export const currentState: StateCode = (process.env.NEXT_PUBLIC_STATE as StateCode) ?? 'dc'

export function skipUnlessState(expected: StateCode): void {
  test.skip(currentState !== expected, `Requires ${expected} build (running ${currentState})`)
}
