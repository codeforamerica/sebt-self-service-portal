/**
 * axe-core Accessibility Testing Setup
 *
 * Development-only runtime accessibility monitoring
 * Reports WCAG violations to console during development
 */

let axeInitialized = false

export async function initAxe() {
  // Only run in development and browser environment
  if (process.env.NODE_ENV !== 'production' && typeof window !== 'undefined' && !axeInitialized) {
    const ReactModule = await import('react')
    const ReactDOMModule = await import('react-dom')
    const axe = await import('@axe-core/react')

    // Create mutable wrapper objects for React 19 ESM compatibility
    // @axe-core/react needs to monkey-patch createElement, but React 19's
    // ESM exports are read-only. Spreading into new objects allows mutation.
    const React = { ...ReactModule }
    const ReactDOM = { ...ReactDOMModule }

    axe.default(React, ReactDOM, 1000, {
      // Configure axe-core rules for USWDS/WCAG compliance
      rules: [
        {
          id: 'color-contrast',
          enabled: true
        },
        {
          id: 'landmark-one-main',
          enabled: true
        },
        {
          id: 'region',
          enabled: true
        }
      ]
    })

    axeInitialized = true
    console.log('🔍 axe-core accessibility monitoring enabled')
  }
}
