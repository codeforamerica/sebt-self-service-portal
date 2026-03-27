'use client'

import { useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { AddressFlowProvider } from '@/features/address'
import { AddressForm } from '@/features/address/components/AddressForm'
import { useHouseholdData } from '@/features/household'
import { Alert } from '@sebt/design-system'

export default function CardReplaceAddressPage() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const searchParams = useSearchParams()
  const { data, isLoading, isError } = useHouseholdData()

  const appNumber = searchParams.get('app')

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !appNumber) {
    return <Alert variant="error">Unable to load address information. Please try again.</Alert>
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('addressUpdateTitle', 'Update your mailing address')}
      </h1>
      <AddressFlowProvider>
        <AddressForm
          initialAddress={data.addressOnFile ?? null}
          redirectPath={`/cards/replace/confirm?app=${encodeURIComponent(appNumber)}`}
        />
      </AddressFlowProvider>
    </div>
  )
}
