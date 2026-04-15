'use client'

import { useDeviceIntelligence } from '@/features/auth/components/device-intelligence'

import { IdProofingForm, type IdOption } from './IdProofingForm'

interface IdProofingWithDiProps {
  idOptions: IdOption[]
  contactLink: string
}

export function IdProofingWithDi({ idOptions, contactLink }: IdProofingWithDiProps) {
  const diSdkKey = process.env.NEXT_PUBLIC_SOCURE_DI_SDK_KEY
  const { getToken } = useDeviceIntelligence(diSdkKey)

  return (
    <IdProofingForm
      idOptions={idOptions}
      contactLink={contactLink}
      getDiToken={getToken}
    />
  )
}
