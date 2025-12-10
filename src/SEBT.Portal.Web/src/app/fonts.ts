/**
 * Font definitions optimized for performance
 * Only load the minimum fonts needed
 */

import { Urbanist } from 'next/font/google'

// Primary font from Figma tokens - only load this one
export const urbanist = Urbanist({
  subsets: ['latin'],
  weight: ['400', '700'], // Reduced from 3 to 2 weights
  variable: '--font-urbanist',
  display: 'optional', // Show fallback immediately, upgrade when font loads
  preload: true,
  fallback: ['system-ui', 'sans-serif']
})
