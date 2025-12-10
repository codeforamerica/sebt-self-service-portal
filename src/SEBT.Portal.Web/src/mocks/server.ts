/**
 * MSW Server for Node.js (Testing Environment)
 *
 * Sets up the mock service worker for Node.js testing environment.
 * Used by Vitest and Playwright tests.
 */
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
