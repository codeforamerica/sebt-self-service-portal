/**
 * State a checker build targets when nothing selects one.
 *
 * `STATE` is the input; `next.config.ts` derives `NEXT_PUBLIC_STATE` from it and
 * overwrites any existing value, so setting `NEXT_PUBLIC_STATE` alone has no
 * effect. Always set both together.
 *
 * Deliberately CO, while the design system's `getState()` defaults to DC — the
 * checker is CO-first and the portal is DC-first. Single-sourced here so the
 * config and the i18n bootstrap can't drift.
 *
 * Import-free so `next.config.ts` can read it without pulling in the design
 * system.
 */
export const CHECKER_DEFAULT_STATE = 'co'
