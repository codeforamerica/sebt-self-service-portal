/**
 * Font Configuration - DC
 *
 * Auto-generated from design tokens.
 * Source: design/states/dc.json
 * DO NOT EDIT DIRECTLY - Regenerate with: pnpm tokens
 *
 * Generated: 2025-12-22T20:32:57.870Z
 */

import { Urbanist } from 'next/font/google'

// Primary font from Figma tokens: urbanist
export const urbanist = Urbanist({
  subsets: ['latin'],
  weight: ['400', '600', '700'],
  variable: '--font-urbanist',
  display: 'optional',
  preload: true,
  fallback: ['system-ui', 'sans-serif']
})

// Export as primaryFont for consistent usage
export const primaryFont = urbanist
