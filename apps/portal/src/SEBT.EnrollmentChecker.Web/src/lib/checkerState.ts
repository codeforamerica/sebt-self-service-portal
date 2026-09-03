/**
 * The state a checker build targets when nothing selects one.
 *
 * `STATE` is the input; `next.config.ts` derives `NEXT_PUBLIC_STATE` from it so
 * browser code can read it. Setting only `NEXT_PUBLIC_STATE` is not enough —
 * the `env` block in `next.config.ts` overwrites it from `STATE`, so a build
 * invoked with just `NEXT_PUBLIC_STATE=dc` still compiles as this default.
 * Always set both together (CI and the deploy workflows do).
 *
 * This is deliberately CO while the portal — and therefore the design system's
 * own `getState()` — defaults to DC. The two apps are DC-first and CO-first
 * respectively, so the values differ on purpose. That divergence only surfaces
 * when `NEXT_PUBLIC_STATE` is unset, which `next.config.ts` prevents for every
 * real build; it is single-sourced here so the config and the i18n bootstrap
 * cannot drift apart.
 *
 * Kept free of imports so `next.config.ts` can read it without pulling the
 * design system into config evaluation.
 */
export const CHECKER_DEFAULT_STATE = 'co'
