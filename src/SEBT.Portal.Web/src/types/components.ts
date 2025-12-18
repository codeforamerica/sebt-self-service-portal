/**
 * Shared component prop types for SEBT Portal
 *
 * These types support multi-state deployment where components
 * receive state configuration via props.
 */

/**
 * Base props for state-aware components
 * @property state - Two-letter state code (e.g., 'dc', 'co')
 */
export interface StateProps {
  state?: string
}

export type HeaderProps = StateProps
export type FooterProps = StateProps
export type HelpSectionProps = StateProps
