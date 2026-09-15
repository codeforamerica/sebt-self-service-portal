/**
 * Next.js instrumentation hook. `register()` runs once per runtime before any
 * app code. OpenTelemetry's Node SDK is not compatible with the edge runtime,
 * so the heavy setup is isolated in instrumentation.node.ts and imported only
 * when running under Node.
 *
 * See https://nextjs.org/docs/app/guides/open-telemetry
 */
export async function register(): Promise<void> {
  assertStateConfigured()

  if (process.env.NEXT_RUNTIME === 'nodejs') {
    await import('./instrumentation.node')
  }
}

/**
 * Fail fast in production when STATE is missing.
 *
 * STATE used to be a required build input, so a missing value broke the build.
 * Now that one artifact serves every state it is supplied per deployment, and an
 * unset value would quietly serve DC branding and theming from, say, the CO
 * environment. Refusing to start surfaces that at deploy time instead.
 */
function assertStateConfigured(): void {
  if (process.env.NODE_ENV !== 'production') {
    return
  }
  if (!process.env.STATE?.trim()) {
    throw new Error(
      'STATE is not set. One artifact serves every state, so each deployment must ' +
        'name its own (e.g. STATE=co) via the ECS task definition or web.config.'
    )
  }
}
