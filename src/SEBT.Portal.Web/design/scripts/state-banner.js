#!/usr/bin/env node
/**
 * Print state configuration banner during build/dev startup.
 * Shows which state is active and where the value came from.
 */
import './load-env.js'

const state = (process.env.STATE || process.env.NEXT_PUBLIC_STATE || 'dc').toUpperCase()
const source = process.env.STATE
  ? 'STATE'
  : process.env.NEXT_PUBLIC_STATE
    ? 'NEXT_PUBLIC_STATE'
    : 'default (dc)'

console.log(`\n  ▸ Building for state: ${state} (via ${source})\n`)
