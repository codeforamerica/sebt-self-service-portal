/**
 * First Vitest setup file: no imports from `@/` so this runs before any module can load `env.ts`.
 * `createEnv()` requires NEXT_PUBLIC_STATE; workers may not inherit shell or Vitest `test.env` reliably.
 */
process.env.NEXT_PUBLIC_STATE ??= 'dc'
