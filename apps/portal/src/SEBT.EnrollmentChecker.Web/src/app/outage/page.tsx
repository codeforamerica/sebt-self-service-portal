import { MaintenancePageContent } from '@sebt/design-system/src/components/MaintenancePage/MaintenancePageContent'
import { getState } from '@sebt/design-system/src/lib/state'

export default function OutagePage() {
  return (
    <MaintenancePageContent
      namespace="maintenanceEnrollmentChecker"
      state={getState()}
    />
  )
}
