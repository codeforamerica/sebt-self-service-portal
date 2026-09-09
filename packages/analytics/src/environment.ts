const NON_PROD_PREFIXES = new Set(['dev', 'staging', 'test', 'qa', 'uat'])

/**
 * Derives the analytics environment label from a browser hostname.
 *
 * page.environment is a client-side telemetry dimension, so the host the user is
 * actually on is the most reliable signal — and, unlike a build-time NEXT_PUBLIC_*
 * value, it resolves correctly for both the server-rendered portal and the
 * statically-exported enrollment checker, and survives promote-the-binary deploys.
 *
 * Non-production hosts carry a known environment prefix as their leftmost label
 * (e.g. dev.co.sebt-portal…) or are local. Every other host — production .gov
 * domains and prefix-less apex hosts — is treated as production.
 */
export function deriveEnvironmentFromHost(hostname: string): string {
  if (hostname === 'localhost' || hostname === '127.0.0.1' || hostname.endsWith('.local')) {
    return 'local'
  }
  const prefix = hostname.split('.')[0] ?? ''
  return NON_PROD_PREFIXES.has(prefix) ? prefix : 'production'
}
