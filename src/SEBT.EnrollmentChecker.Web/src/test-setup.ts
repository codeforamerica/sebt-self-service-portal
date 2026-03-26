import '@testing-library/jest-dom'
import { afterAll, afterEach, beforeAll } from 'vitest'

// Initialize i18n before tests (mirrors Providers.tsx in production)
import '@/lib/i18n-init'
import { server } from './mocks/server'

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
