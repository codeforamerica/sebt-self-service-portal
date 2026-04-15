const COOLDOWN_DAYS = 14
const COOLDOWN_MS = COOLDOWN_DAYS * 24 * 60 * 60 * 1000

/**
 * Returns true if the card was requested within the last 14 days.
 * Timestamp-only check, independent of current card status (D5).
 */
export function isWithinCooldownPeriod(cardRequestedAt: string | null | undefined): boolean {
  if (!cardRequestedAt) return false

  const requestedDate = new Date(cardRequestedAt)
  if (isNaN(requestedDate.getTime())) return false

  return Date.now() - requestedDate.getTime() < COOLDOWN_MS
}
