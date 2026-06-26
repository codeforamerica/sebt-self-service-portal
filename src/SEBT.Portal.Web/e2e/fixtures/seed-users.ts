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
