import '@testing-library/jest-dom'
import { afterAll, afterEach, beforeAll } from 'vitest'

import '@/lib/i18n'
import { server } from '../src/mocks/server'

// Setup file for Vitest
// This runs before all tests

// Establish API mocking before all tests
beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))

// Reset any request handlers that we may add during tests
afterEach(() => server.resetHandlers())

// Clean up after tests are finished
afterAll(() => server.close())
