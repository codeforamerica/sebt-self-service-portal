/** MSW server for the Vitest suite (Playwright specs mock the API with `page.route`). */
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
