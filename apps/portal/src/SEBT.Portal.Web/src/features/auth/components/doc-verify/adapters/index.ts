export { MockDocVAdapter } from './mock-adapter'
export { SocureDocVAdapter } from './socure-adapter'
export type { DocVAdapter, DocVAdapterConfig } from './types'

import { MockDocVAdapter } from './mock-adapter'
import { SocureDocVAdapter } from './socure-adapter'
import type { DocVAdapter } from './types'

export function createDocVAdapter(mockSocure: boolean): DocVAdapter {
  if (mockSocure) {
    return new MockDocVAdapter()
  }
  return new SocureDocVAdapter()
}
