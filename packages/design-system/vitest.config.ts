import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
    // content/scripts is listed by name: generate-locales.test.js there is a
    // standalone node:assert script, not a Vitest suite.
    include: [
      'src/**/*.test.{ts,tsx}',
      'design/scripts/**/*.test.{js,ts}',
      'content/scripts/validate-content*.test.js',
      'content/scripts/referenced-keys.test.js'
    ],
    exclude: ['node_modules/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'json-summary', 'html'],
      exclude: [
        'node_modules/',
        '**/*.config.*',
        '**/*.d.ts',
        '**/*.test.{ts,tsx}'
      ]
    }
  },
})
