'use client'

import { useDataLayer } from '@sebt/analytics'
import { useEffect } from 'react'

import { useAuth } from '@/features/auth/context'
import { IdProofingStatus, isIdProofedForAnalytics } from '@/lib/idProofingStatus'

const ANALYTICS_SCOPE: string[] = ['default', 'analytics']

/**
 * Syncs user-level data from the current session into the data layer.
 * Place in the authenticated layout so it runs for all protected pages.
 */
export function useUserDataSync() {
  const { session, isAuthenticated } = useAuth()
  const { setUserData } = useDataLayer()

  useEffect(() => {
    setUserData('authenticated', isAuthenticated, ANALYTICS_SCOPE)

    if (!session) return

    if (session.ial) {
      const ialNumeric = session.ial === '1plus' ? 1.5 : Number(session.ial)
      setUserData('identity_assurance_level', ialNumeric, ANALYTICS_SCOPE)
    }

    const idProofingStatus = session.idProofingStatus
    setUserData(
      'id_proofed',
      isIdProofedForAnalytics(idProofingStatus, session.idProofingCompletedAt),
      ANALYTICS_SCOPE
    )
    setUserData(
      'has_dob',
      idProofingStatus != null && idProofingStatus !== IdProofingStatus.NotStarted,
      ANALYTICS_SCOPE
    )

    // TODO: user.coloading_status — requires backend to expose in JWT or household API
    // TODO: user.opt_in_email — requires backend to expose
    // TODO: user.opt_in_text — requires backend to expose
    // TODO: user.has_active_application — derivable from household API once schema is confirmed
  }, [session, isAuthenticated, setUserData])
}

/** Client component wrapper for use in server component layouts. */
export function UserDataSync() {
  useUserDataSync()
  return null
}
