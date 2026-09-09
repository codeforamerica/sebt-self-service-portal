/** Prefix for all household React Query keys; use for invalidation across users. */
export const HOUSEHOLD_DATA_QUERY_KEY_PREFIX = ['householdData'] as const

/** Household data cache key scoped to the authenticated portal user. */
export function householdDataQueryKey(userId: string) {
  return [...HOUSEHOLD_DATA_QUERY_KEY_PREFIX, userId] as const
}

/** Deferred card-details fetch key scoped to the authenticated portal user. */
export function householdCardDetailsQueryKey(userId: string) {
  return [...HOUSEHOLD_DATA_QUERY_KEY_PREFIX, userId, 'cardDetails'] as const
}
