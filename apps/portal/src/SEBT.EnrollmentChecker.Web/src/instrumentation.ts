/**
 * Next.js instrumentation hook. `register()` runs once per runtime before any
 * app code. OpenTelemetry's Node SDK is not compatible with the edge runtime,
 * so the heavy setup is isolated in instrumentation.node.ts and imported only
 * when running under Node.
 *
 * This app ships in two shapes: an SSR standalone server (where this provides
 * live server telemetry) and a static export (where there is no server, so
 * register only runs once at build time and — with no endpoint — is a no-op).
 *
 * See https://nextjs.org/docs/app/guides/open-telemetry
 */
export async function register(): Promise<void> {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    await import('./instrumentation.node')
  }
}
