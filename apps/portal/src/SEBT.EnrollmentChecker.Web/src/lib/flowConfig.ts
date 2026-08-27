import { getState, type StateCode } from '@sebt/design-system/src/lib/state'

// Per-state shape of the check flow.

export interface FlowConfig {
  /**
   * Whether the flow includes a review step between the child form and the
   * results.
   *
   * States with a review step collect children, confirm them on /review, then
   * submit. States without it submit straight from the form, and /review is not
   * part of their flow at all.
   */
  useReviewStep: boolean

  /**
   * Whether a check that returns no usable record gets its own outcome.
   *
   * States that set this false fold those results into the not-enrolled
   * outcome, because their backend does not separate "no match at all" from
   * "found but not matched" — unmatched input already lands on not-enrolled, so
   * a distinct no-results screen would describe a state that cannot occur.
   */
  distinguishNoResults: boolean

  /**
   * How the results read.
   *
   * `household` summarises several children at once: one neutral heading, each
   * child named under the outcome that applies to them, and next steps for the
   * household. `singleOutcome` answers for one child, so the outcome itself is
   * the heading and there is no one to name.
   */
  resultsLayout: ResultsLayout
}

export type ResultsLayout = 'household' | 'singleOutcome'

const flowConfigs: Record<StateCode, FlowConfig> = {
  dc: { useReviewStep: false, distinguishNoResults: false, resultsLayout: 'singleOutcome' },
  co: { useReviewStep: true, distinguishNoResults: true, resultsLayout: 'household' }
}

/** Flow config for the active state. */
export function getFlowConfig(): FlowConfig {
  const state = getState()
  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  return flowConfigs[state] ?? flowConfigs.dc
}

/**
 * Whether the results offer a way to start another check.
 *
 * Derived rather than configured: a flow with no review step covers one child
 * per check, so it needs somewhere to begin the next one. A flow with a review
 * step collects the whole household before submitting and has nothing to
 * return for.
 */
export function allowsSequentialChecks(): boolean {
  return !getFlowConfig().useReviewStep
}
