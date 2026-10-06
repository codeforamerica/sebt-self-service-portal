import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
    include: [
      'src/**/*.test.{ts,tsx}',
      'design/scripts/**/*.test.{js,ts}',
      'content/scripts/**/*.test.js'
    ],
    // generate-locales.test.js is a standalone node:assert script, not a Vitest suite.
    exclude: ['node_modules/**', 'content/scripts/generate-locales.test.js'],
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
