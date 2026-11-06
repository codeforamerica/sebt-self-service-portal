import type { PageColumnProps } from './types'

/**
 * Page frame for a screen's content: USWDS section, the 1024px grid-container,
 * and a 480px column centered inside it. The column is an inner element because
 * a `maxw-*` utility on the grid-container itself caps the padded box instead.
 */
export function PageColumn({ children }: PageColumnProps) {
  return (
    <div className="usa-section">
      <div className="grid-container">
        <div className="maxw-mobile-lg margin-x-auto">{children}</div>
      </div>
    </div>
  )
}
