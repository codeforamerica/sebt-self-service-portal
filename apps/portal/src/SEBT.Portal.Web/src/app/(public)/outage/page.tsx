'use client'

import { getState } from '@sebt/design-system'
import { MaintenancePageContent } from '@sebt/design-system/client'

export default function OutagePage() {
  return (
    <MaintenancePageContent
      namespace="maintenancePortal"
      state={getState()}
    />
  )
}
