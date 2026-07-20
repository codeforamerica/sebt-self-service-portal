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

/** Co-loaded user with enrolled SNAP/TANF cases and an approved application (Maria MartinezMOCK). */
export const DC_CO_LOADED_EMAIL = dcSeedEmail('co-loaded')

/** Co-loaded user with enrolled cases but zero applications (DC-402 applications CTA hidden). */
export const DC_CO_LOADED_NO_APPLICATION_EMAIL = dcSeedEmail('co-loaded-no-application')

/** Co-loaded user with completed ID proofing but zero enrolled children and zero applications. */
export const DC_CO_LOADED_NO_CHILDREN_EMAIL = dcSeedEmail('co-loaded-no-children')

/** Mixed-eligibility household: Summer Ebt (Aiden) + SNAP co-loaded (Lily); Lily is filtered from the payload. */
export const DC_MIXED_EMAIL = dcSeedEmail('dc-mixed')

/** User with expired ID proofing — post-OTP routes to id-proofing, not the dashboard. */
export const DC_EXPIRED_ID_PROOFING_EMAIL = dcSeedEmail('expired')

/** Denied-application household; post-OTP routes to id-proofing. */
export const DC_DENIED_EMAIL = dcSeedEmail('denied')

/** Cancelled-application household; post-OTP routes to id-proofing. */
export const DC_CANCELLED_EMAIL = dcSeedEmail('cancelled')

/** Co-loaded SNAP household pending ID proofing — post-OTP routes to id-proofing. */
export const DC_CO_LOADED_PENDING_ID_PROOFING_EMAIL = dcSeedEmail('co-loaded-pending-id-proofing')

/** On-file SNAP account ID for co-loaded-pending seed persona (digit-only for id-proofing UI). */
export const DC_CO_LOADED_PENDING_SNAP_ACCOUNT_ID = '87654321'
