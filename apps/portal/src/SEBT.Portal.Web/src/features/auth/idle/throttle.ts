/**
 * Returns a throttled wrapper that invokes `fn` at most once per `intervalMs`.
 *
 * Leading-edge: the first call after a quiet period fires immediately, and
 * subsequent calls within the interval are dropped (not deferred). This is the
 * right shape for activity tracking — we want the *first* event of a burst to
 * register, not the last.
 */
export function throttle<TArgs extends unknown[]>(
  fn: (...args: TArgs) => void,
  intervalMs: number
): (...args: TArgs) => void {
  let lastInvokedAt = -Infinity
  return (...args: TArgs) => {
    const now = Date.now()
    if (now - lastInvokedAt >= intervalMs) {
      lastInvokedAt = now
      fn(...args)
    }
  }
}
