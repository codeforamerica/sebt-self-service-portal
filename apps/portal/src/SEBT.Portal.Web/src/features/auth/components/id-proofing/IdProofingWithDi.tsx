'use client'

import { useDeviceIntelligence } from '@/features/auth/components/device-intelligence'
import { useAuth } from '@/features/auth/context'

import { IdProofingForm, type IdOption } from './IdProofingForm'

interface IdProofingWithDiProps {
  idOptions: IdOption[]
  coLoadedIdOptions: IdOption[]
  contactLink: string
}

export function IdProofingWithDi({
  idOptions,
  coLoadedIdOptions,
  contactLink
}: IdProofingWithDiProps) {
  const diSdkKey = process.env.NEXT_PUBLIC_SOCURE_DI_SDK_KEY
  const { getToken } = useDeviceIntelligence(diSdkKey)
  const { session } = useAuth()
  const options = session?.isCoLoaded ? coLoadedIdOptions : idOptions

  return (
    <IdProofingForm
      idOptions={options}
      contactLink={contactLink}
      getDiToken={getToken}
    />
  )
}
