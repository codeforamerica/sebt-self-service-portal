import { Spinner } from './Spinner';

export interface ProcessingIndicatorProps {
  isProcessing: boolean;
  /** Localized announcement, e.g. t('processing'). Required: an empty live
   * region would leave screen-reader users with no processing signal beyond
   * the button's aria-busy. */
  label: string;
  className?: string;
}

// Rendered at the end of a form's button row: the spinner appears after the
// buttons while a submission is in flight. The status region stays mounted
// while idle so assistive tech registers the live region before content is
// inserted into it. Deliberately no aria-busy here: on a live region it tells
// assistive tech to DEFER announcing updates, which would suppress the one
// announcement this region exists to make. The submit button already carries
// aria-busy via Button's isLoading.
export function ProcessingIndicator({ isProcessing, label, className }: ProcessingIndicatorProps) {
  return (
    <div role="status" aria-live="polite" className={className}>
      {isProcessing && (
        <>
          <Spinner />
          <span className="usa-sr-only">{label}</span>
        </>
      )}
    </div>
  );
}
