/**
 * Reads env used by IalGuard without importing `@/env`, so Vitest (and any module graph)
 * does not need `createEnv()` to run when only these flags are needed.
 */

export function isDebugRepeatOidcStepUp(): boolean {
  return (
    process.env.NODE_ENV === 'development' &&
    process.env.NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP === 'true'
  )
}

/** Raw value for {@link parseIdProofingMaxAgeYears} in `jwt.ts` (default 5 when unset). */
export function getCoIdProofingMaxAgeYearsRaw(): string | undefined {
  return process.env.NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS
}
