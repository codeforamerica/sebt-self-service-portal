/**
 * SkipNav Component
 *
 * WCAG 2.1 accessibility requirement for keyboard navigation.
 * Allows users to skip repetitive navigation and jump directly to main content.
 *
 * How it works:
 * 1. Link is visually hidden by default (usa-skipnav class)
 * 2. When user presses Tab on page load, link becomes visible
 * 3. Clicking it jumps to <main id="main-content"> (skipping header/nav)
 *
 * Required for Section 508 compliance (government accessibility standards).
 *
 * Note: Uses hardcoded text per USWDS standard - "Skip to main content" is the
 * standard accessibility phrase across all government sites and doesn't need
 * state-specific or language-specific variations from the CMS.
 *
 * @see https://designsystem.digital.gov/components/skipnav/
 */

export function SkipNav() {
  return (
    <a
      className="usa-skipnav"
      href="#main-content"
    >
      Skip to main content
    </a>
  )
}
