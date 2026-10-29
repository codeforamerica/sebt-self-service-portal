import { test } from '@playwright/test'
// Playwright runs outside Next's module aliases; the package exports map allows ./src/*.
import {
  getState,
  getStateConfig,
  type LoginMode,
  type StateCode
} from '@sebt/design-system/src/lib/state'

export type { StateCode }

// Same resolution as the app: STATE or NEXT_PUBLIC_STATE, lowercased, empty treated as absent.
export const currentState: StateCode = getState()

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
