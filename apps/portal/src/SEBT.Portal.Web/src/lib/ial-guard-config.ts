/**
 * Reads env used by IalGuard without importing `@/env`, so Vitest (and any module graph)
 * does not need `createEnv()` to run when only these flags are needed.
 */

export function isDebugRepeatOidcStepUp(debugRepeatOidcStepUp: boolean): boolean {
  return process.env.NODE_ENV === 'development' && debugRepeatOidcStepUp
}
