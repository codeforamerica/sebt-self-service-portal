'use client'

import { useDataLayer } from '@sebt/analytics'
import { useEffect } from 'react'

import { useAuth } from '@/features/auth/context'
import { decodeJwtPayload, getIalFromToken } from '@/lib/jwt'

const ANALYTICS_SCOPE: string[] = ['default', 'analytics']

// Mirrors SEBT.Portal.Core.Models.Auth.IdProofingStatus enum values from the JWT claim.
// JWT serializes as Integer32 (JSON number), but we parse defensively via Number() in case of string.
const ID_PROOFING_NOT_STARTED = 0
const ID_PROOFING_COMPLETED = 2

/**
 * Syncs user-level data from the JWT and auth context into the data layer.
 * Place in the authenticated layout so it runs for all protected pages.
 */
export function useUserDataSync() {
  const { token, isAuthenticated } = useAuth()
  const { setUserData } = useDataLayer()

  useEffect(() => {
    setUserData('authenticated', isAuthenticated, ANALYTICS_SCOPE)

    if (!token) return

    // Identity assurance level
    const ial = getIalFromToken(token)
    if (ial) {
      const ialNumeric = ial === '1plus' ? 1.5 : Number(ial)
      setUserData('identity_assurance_level', ialNumeric, ANALYTICS_SCOPE)
    }

    // ID proofing status — parse via Number() to handle both string and numeric JWT values
    const payload = decodeJwtPayload(token)
    if (payload) {
      const rawStatus = payload.id_proofing_status
      const idProofingStatus = rawStatus != null ? Number(rawStatus) : undefined
      const completedAt = payload.id_proofing_completed_at
      setUserData(
        'id_proofed',
        idProofingStatus === ID_PROOFING_COMPLETED || !!completedAt,
        ANALYTICS_SCOPE
      )
      setUserData(
        'has_dob',
        idProofingStatus !== ID_PROOFING_NOT_STARTED && idProofingStatus !== undefined,
        ANALYTICS_SCOPE
      )
    }

    // TODO: user.coloading_status — requires backend to expose in JWT or household API
    // TODO: user.opt_in_email — requires backend to expose
    // TODO: user.opt_in_text — requires backend to expose
    // TODO: user.has_active_application — derivable from household API once schema is confirmed
  }, [token, isAuthenticated, setUserData])
}

/** Client component wrapper for use in server component layouts. */
export function UserDataSync() {
  useUserDataSync()
  return null
}
