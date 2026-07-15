import { stateResources } from '@/lib/generated-locale-resources'
import { OutagePageContent } from '@sebt/design-system'

export default function OutagePage() {
  return <OutagePageContent resources={stateResources} />
}
