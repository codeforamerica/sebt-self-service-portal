import path from 'path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // @/env createEnv() requires NEXT_PUBLIC_STATE before modules that import it load (auth barrel → IalGuard).
    env: {
      NEXT_PUBLIC_STATE: 'dc'
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
      reporter: ['text', 'json', 'html'],
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
