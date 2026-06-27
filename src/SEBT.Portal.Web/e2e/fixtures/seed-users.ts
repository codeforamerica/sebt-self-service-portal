/**
 * Seeded DC test personas (Seeding:EmailPattern in appsettings.dc.json).
 * Must stay in sync with SeedScenarios in the backend.
 */
export const DC_SEED_EMAIL_PATTERN = 'sebt.dc+{0}@codeforamerica.org'

export function dcSeedEmail(scenarioName: string): string {
  return DC_SEED_EMAIL_PATTERN.replace('{0}', scenarioName)
}

/** IAL1+ user with completed ID proofing and mock household children (John/Jane Doe). */
export const DC_VERIFIED_EMAIL = dcSeedEmail('verified')

/** Non-co-loaded user with ID proofing status InProgress — post-OTP routes to id-proofing. */
export const DC_ID_PROOF_IN_PROGRESS_EMAIL = dcSeedEmail('id-proof-in-progress')

/** Co-loaded user with enrolled cases but zero applications (DC-402 applications CTA hidden). */
export const DC_CO_LOADED_NO_APPLICATION_EMAIL = dcSeedEmail('co-loaded-no-application')
