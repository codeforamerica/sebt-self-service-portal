import { stateResources } from '@/lib/generated-locale-resources'
import type { StateResources } from '@sebt/design-system'
import { OutagePageContent } from '@sebt/design-system/src/components/OutagePage/OutagePageContent'

export default function OutagePage() {
  return <OutagePageContent resources={stateResources as StateResources} />
}
