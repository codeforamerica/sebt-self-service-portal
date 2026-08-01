import type { ReactNode } from 'react';

export interface ProcessingFieldsetProps {
  isProcessing: boolean;
  children: ReactNode;
  className?: string;
}

// Disables every descendant form control natively via <fieldset disabled>.
// The 50% fade must live here on the ancestor: USWDS pins `opacity: 1` on
// disabled controls, and opacity composites the whole subtree. Keep buttons,
// error surfaces, and the spinner OUTSIDE this wrapper at the call site, or
// they render washed out.
export function ProcessingFieldset({ isProcessing, children, className }: ProcessingFieldsetProps) {
  const classes = ['usa-fieldset', isProcessing && 'opacity-50', className]
    .filter(Boolean)
    .join(' ');
  return (
    <fieldset disabled={isProcessing} className={classes}>
      {children}
    </fieldset>
  );
}
