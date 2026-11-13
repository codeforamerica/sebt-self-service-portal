import { withGuidepup } from '@guidepup/playwright'
import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/**
 * Screen reader runs: a real VoiceOver drives a headed browser and the spec
 * asserts what was spoken. Serial and headed by design (withGuidepup), so this
 * stays out of the default `playwright test` run. One-time machine setup:
 * VoiceOver Utility > General > "Allow VoiceOver to be controlled with
 * AppleScript", plus Accessibility permission for the terminal that runs it.
 */
export default defineConfig(
  withGuidepup({
    testDir: './e2e/screen-reader',
    timeout: 5 * 60 * 1000,
    reportSlowTests: null,
    retries: 0,
    reporter: 'list',
    use: {
      baseURL: process.env.BASE_URL || 'http://localhost:3000',
      trace: 'retain-on-failure',
      screenshot: 'only-on-failure'
    },
    projects: [{ name: 'voiceover-chromium', use: { ...devices['Desktop Chrome'] } }],
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
)
