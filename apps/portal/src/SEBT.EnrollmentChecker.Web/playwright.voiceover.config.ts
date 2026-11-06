import { withGuidepup } from '@guidepup/playwright'
import { defineConfig, devices } from '@playwright/test'

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
      baseURL: 'http://localhost:3001',
      trace: 'retain-on-failure'
    },
    projects: [{ name: 'voiceover-chromium', use: { ...devices['Desktop Chrome'] } }],
    webServer: {
      command: 'pnpm dev',
      url: 'http://localhost:3001',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        PORT: '3001',
        NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE ?? 'co',
        NEXT_PUBLIC_PORTAL_URL: 'http://localhost:3000',
        NEXT_PUBLIC_APPLICATION_URL: 'http://localhost:3000/apply',
        NEXT_PUBLIC_APPLICATION_URL_PEAK_PARAMS: 'true',
        SKIP_ENV_VALIDATION: 'true'
      }
    }
  })
)
