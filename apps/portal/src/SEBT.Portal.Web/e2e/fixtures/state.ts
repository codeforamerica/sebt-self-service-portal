import { test } from '@playwright/test'

export type StateCode = 'dc' | 'co'

export const currentState: StateCode = (process.env.NEXT_PUBLIC_STATE as StateCode) ?? 'dc'

export function skipUnlessState(expected: StateCode): void {
  test.skip(currentState !== expected, `Requires ${expected} build (running ${currentState})`)
}
