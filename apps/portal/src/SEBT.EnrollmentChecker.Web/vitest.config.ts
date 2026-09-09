import path from 'path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // i18n-init reads NEXT_PUBLIC_STATE at import time (before setupFiles code runs),
    // so the state must be set here rather than in test-setup.ts. The checker is
    // CO-first (matches .env.local and the i18n-init default).
    env: {
      NEXT_PUBLIC_STATE: 'co'
    },
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    globals: true,
    css: true,
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**', '.next/**'],
  },
  resolve: {
    alias: {
      '@/content': path.resolve(__dirname, './content'),
      '@': path.resolve(__dirname, './src')
    }
  }
})
