import '@testing-library/jest-dom'
import { afterAll, afterEach, beforeAll } from 'vitest'

// Initialize i18n before tests (mirrors Providers.tsx in production)
import '@/lib/i18n-init'
import { server } from './mocks/server'

process.env.NEXT_PUBLIC_STATE ??= 'dc'
process.env.NEXT_PUBLIC_PORTAL_URL ??= 'https://portal.example.gov'
process.env.NEXT_PUBLIC_APPLICATION_URL ??= 'https://portalapp.example.gov'

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
