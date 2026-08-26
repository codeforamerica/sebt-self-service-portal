import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

const isFullStack = process.env.E2E_FULL_STACK === '1'
const integrationTestIgnore = '**/integration/**'

/**
 * Playwright E2E Testing Configuration
 * Cross-browser testing with mobile viewport support
 * Set SKIP_WEB_SERVER=1 in CI when the server is already running (for Pa11y)
 * Set E2E_FULL_STACK=1 for integration specs that require API + Mailpit (see e2e/integration/)
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  ...(process.env.CI ? { workers: 1 } : {}),
  globalTimeout: process.env.CI ? 600_000 : 0,
  reporter: process.env.CI ? [['list'], ['html']] : 'html',
  ...(isFullStack ? { globalSetup: './e2e/global-setup.ts' } : {}),

  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure'
  },

  projects: [
    {
      name: 'chromium',
      testIgnore: integrationTestIgnore,
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'firefox',
      testIgnore: integrationTestIgnore,
      use: { ...devices['Desktop Firefox'] }
    },
    {
      name: 'webkit',
      testIgnore: integrationTestIgnore,
      use: { ...devices['Desktop Safari'] }
    },
    {
      name: 'Mobile Chrome',
      testIgnore: integrationTestIgnore,
      use: { ...devices['Pixel 5'] }
    },
    {
      name: 'Mobile Safari',
      testIgnore: integrationTestIgnore,
      use: { ...devices['iPhone 12'] }
    },
    ...(isFullStack
      ? [
          {
            name: 'chromium-integration-dc',
            testMatch: '**/integration/dc/**/*.spec.ts',
            use: { ...devices['Desktop Chrome'] }
          },
          {
            name: 'chromium-integration-co',
            testMatch: '**/integration/co/**/*.spec.ts',
            use: { ...devices['Desktop Chrome'] }
          }
        ]
      : [])
  ],

  // Omit webServer when SKIP_WEB_SERVER is set
  ...(process.env.SKIP_WEB_SERVER
    ? {}
    : {
        webServer: {
          command: 'pnpm dev',
          url: process.env.BASE_URL || 'http://localhost:3000',
          cwd: path.resolve(__dirname, '../..'),
          reuseExistingServer: !process.env.CI
        }
      })
})
