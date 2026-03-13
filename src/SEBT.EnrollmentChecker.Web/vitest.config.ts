import path from 'path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    globals: true,
    css: true,
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**', '.next/**'],
  },
  resolve: {
    alias: {
      // More-specific aliases must come before '@' so they are matched first
      '@/content': path.resolve(__dirname, './content'),
      '@': path.resolve(__dirname, './src'),
      '@sebt/design-system': path.resolve(__dirname, '../../packages/design-system/src/index.ts')
    }
  }
})
