/**
 * Server-side translations for SEBT Portal
 *
 * This module provides translation utilities for Server Components.
 * For Client Components, use react-i18next via the I18nProvider.
 *
 * Import Management:
 * All locale imports are auto-generated in generated-locale-resources.ts
 * by content/scripts/generate-locales.js. Adding a new namespace JSON file
 * only requires running `pnpm copy:generate` — no manual import edits needed.
 */

import { stateResources, type Namespace } from './generated-locale-resources'

// Get state from environment
const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase() as
  | 'dc'
  | 'co'

// Extract English-only resources for server-side (language switching happens client-side)
// eslint-disable-next-line security/detect-object-injection -- state is validated at build time, fallback guards against misconfiguration
const resources = (stateResources[state]?.en ?? stateResources.dc.en) as Record<
  Namespace,
  Record<string, string>
>

/**
 * Get a translation function for a specific namespace
 * For use in Server Components
 */
export function getTranslations(namespace: Namespace) {
  // eslint-disable-next-line security/detect-object-injection -- namespace is typed Namespace
  const translations = resources[namespace]
  return (key: string, defaultValue?: string): string => {
    // eslint-disable-next-line security/detect-object-injection -- key is from trusted translation files
    return translations[key] ?? defaultValue ?? key
  }
}
