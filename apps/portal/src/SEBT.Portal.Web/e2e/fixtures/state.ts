import { test } from '@playwright/test'
// Playwright runs outside Next's module aliases; the package exports map allows ./src/*.
import { getStateConfig, type LoginMode, type StateCode } from '@sebt/design-system/src/lib/state'

export type { StateCode }

export const currentState: StateCode = (process.env.NEXT_PUBLIC_STATE as StateCode) ?? 'dc'

const currentLoginMode: LoginMode = getStateConfig(currentState).loginMode

export function skipUnlessState(expected: StateCode): void {
  test.skip(currentState !== expected, `Requires ${expected} build (running ${currentState})`)
}

export function skipUnlessLoginMode(expected: LoginMode): void {
  test.skip(
    currentLoginMode !== expected,
    `Requires ${expected} login mode (running ${currentState}, ${currentLoginMode})`
  )
}
