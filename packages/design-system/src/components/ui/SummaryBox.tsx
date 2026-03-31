import type { HTMLAttributes, ReactNode } from 'react'

import styles from './SummaryBox.module.css'

export interface SummaryBoxProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode
}

/**
 * Bordered info panel for short supporting copy.
 */
export function SummaryBox({ children, className = '', ...rest }: SummaryBoxProps) {
  const combined = `${styles.summaryBox} ${className}`.trim()
  return (
    <div
      className={combined}
      {...rest}
    >
      {children}
    </div>
  )
}
