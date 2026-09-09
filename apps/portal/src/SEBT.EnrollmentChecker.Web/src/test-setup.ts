import '@testing-library/jest-dom'
import { afterAll, afterEach, beforeAll } from 'vitest'

// Initialize i18n before tests (mirrors Providers.tsx in production)
import '@/lib/i18n-init'
import { server } from './mocks/server'

// NEXT_PUBLIC_STATE is set in vitest.config.ts (test.env) so it's available
// when i18n-init runs at import time above
process.env.NEXT_PUBLIC_PORTAL_URL ??= 'https://portal.example.gov'
process.env.NEXT_PUBLIC_APPLICATION_URL ??= 'https://portalapp.example.gov'

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
