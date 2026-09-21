'use client'

import { useDeviceIntelligence } from '@/features/auth/components/device-intelligence'
import { useAuth } from '@/features/auth/context'
import { useFeatureFlag, useFeatureFlagsStatus } from '@/features/feature-flags'
import { useRuntimeConfig } from '@/providers'

import { IdProofingForm, type IdOption } from './IdProofingForm'

interface IdProofingWithDiProps {
  idOptions: IdOption[]
  coLoadedIdOptions: IdOption[]
  /** Offered after "No" when enable_socure_snap_tanf_question is on. */
  snapTanfIdOptions: IdOption[]
  /** The case number asked for after "Yes" when enable_socure_snap_tanf_question is on. */
  snapTanfOption: IdOption
  contactLink: string
}

export function IdProofingWithDi({
  idOptions,
  coLoadedIdOptions,
  snapTanfIdOptions,
  snapTanfOption,
  contactLink
}: IdProofingWithDiProps) {
  const diSdkKey = useRuntimeConfig().socureDiSdkKey
  const { getToken } = useDeviceIntelligence(diSdkKey)
  const { session } = useAuth()
  const askSnapTanfQuestion = useFeatureFlag('enable_socure_snap_tanf_question')
  const { isLoading: flagsLoading } = useFeatureFlagsStatus()

  // Hold the form until flags resolve, so no one starts on one design and has it swapped for the other.
  if (flagsLoading) return null

  // Every user answers the question. Co-loaded status is only learned from a SNAP/TANF match, so a
  // first-time co-loaded user's session cannot be used to pick their options.
  if (askSnapTanfQuestion) {
    return (
      <IdProofingForm
        idOptions={snapTanfIdOptions}
        snapTanfOption={snapTanfOption}
        contactLink={contactLink}
        getDiToken={getToken}
      />
    )
  }

  const options = session?.isCoLoaded ? coLoadedIdOptions : idOptions

  return (
    <IdProofingForm
      idOptions={options}
      contactLink={contactLink}
      getDiToken={getToken}
    />
  )
}
