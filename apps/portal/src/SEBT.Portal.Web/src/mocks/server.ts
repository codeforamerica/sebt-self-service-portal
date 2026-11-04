/** MSW server shared by the Vitest and Playwright suites. */
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
