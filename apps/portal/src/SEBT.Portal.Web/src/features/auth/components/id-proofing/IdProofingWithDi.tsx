'use client'

import { useDeviceIntelligence } from '@/features/auth/components/device-intelligence'

import { IdProofingForm, type IdOption } from './IdProofingForm'

interface IdProofingWithDiProps {
  idOptions: IdOption[]
  snapTanfOption: IdOption
  contactLink: string
}

export function IdProofingWithDi({
  idOptions,
  snapTanfOption,
  contactLink
}: IdProofingWithDiProps) {
  const diSdkKey = process.env.NEXT_PUBLIC_SOCURE_DI_SDK_KEY
  const { getToken } = useDeviceIntelligence(diSdkKey)

  // Every user answers the SNAP/TANF question. Co-loaded status is only learned from a SNAP/TANF
  // match, so a first-time co-loaded user's session cannot be used to pick their options.
  return (
    <IdProofingForm
      idOptions={idOptions}
      snapTanfOption={snapTanfOption}
      contactLink={contactLink}
      getDiToken={getToken}
    />
  )
}
