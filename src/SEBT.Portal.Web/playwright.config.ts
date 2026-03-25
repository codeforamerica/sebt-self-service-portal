import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/**
 * Playwright E2E Testing Configuration
 * Cross-browser testing with mobile viewport support
 * Set SKIP_WEB_SERVER=1 in CI when the server is already running (for Pa11y)
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  ...(process.env.CI ? { workers: 1 } : {}),
  reporter: 'html',

  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure'
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] }
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] }
    },
    {
      name: 'Mobile Chrome',
      use: { ...devices['Pixel 5'] }
    },
    {
      name: 'Mobile Safari',
      use: { ...devices['iPhone 12'] }
    }
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
