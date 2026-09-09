import path from 'path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // @/env createEnv() requires NEXT_PUBLIC_STATE; SKIP_ENV_VALIDATION skips strict OIDC checks during tests.
    // SKIP_ENV_VALIDATION also bypasses Zod defaults, so BACKEND_URL must be set explicitly here
    // (mirroring env.ts's default) for the API proxy route tests.
    env: {
      SKIP_ENV_VALIDATION: '1',
      NEXT_PUBLIC_STATE: 'dc',
      BACKEND_URL: 'http://localhost:5280'
    },
    environment: 'jsdom',
    setupFiles: ['./src/test-env-preload.ts', './src/test-setup.ts'],
    globals: true,
    css: true,
    // Support co-located tests: tests next to components in src/
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**', '.next/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'json-summary', 'html'],
      exclude: [
        'node_modules/',
        'e2e/',
        '**/*.config.*',
        '**/*.d.ts',
        '.next/',
        // Exclude test files from coverage
        '**/*.test.{ts,tsx}'
      ]
    }
  },
  resolve: {
    alias: {
      '@/design': path.resolve(__dirname, './design'),
      '@/content': path.resolve(__dirname, './content'),
      '@': path.resolve(__dirname, './src')
    }
  }
})
