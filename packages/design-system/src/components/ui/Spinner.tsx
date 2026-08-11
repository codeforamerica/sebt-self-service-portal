export interface SpinnerProps {
  className?: string;
}

// Visual-only: always aria-hidden. Callers own the announcement (a role="status"
// live region with real text), matching the LoadingInterstitial pattern.
export function Spinner({ className }: SpinnerProps) {
  const classes = className ? `usa-spinner ${className}` : 'usa-spinner';
  return <span className={classes} aria-hidden="true" />;
}
