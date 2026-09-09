// Path-traversal guard for the /api/* catch-all proxy.
//
// Next.js hands the catch-all `path` to the route as segments split on real "/",
// so encoded separators (%2f, %5c) and encoded dots (%2e) stay inside a single
// segment and slip past a literal "segment === '..'" check. The smuggled ".." then
// resolves outside /api/ during URL construction or backend path normalization —
// e.g. /api/x%2f..%2f..%2fhealth reaching the backend's /health and /swagger.
// Each segment is validated raw and decoded, and the final URL is asserted to stay
// under /api/ as a backstop.

const SEPARATORS = ['/', '\\']

function isUnsafeSegment(segment: string): boolean {
  let decoded: string
  try {
    decoded = decodeURIComponent(segment)
  } catch {
    // Malformed percent-encoding cannot be validated — reject it.
    return true
  }

  return [segment, decoded].some(
    (value) =>
      value === '.' ||
      value.includes('..') ||
      SEPARATORS.some((separator) => value.includes(separator))
  )
}

/**
 * Resolves the backend URL for a proxied /api/* request, or returns null when the
 * request must be rejected (HTTP 400).
 *
 * @param path Catch-all segments from the Next.js route (already split on real "/").
 * @param backendUrl Base URL of the .NET backend.
 * @param search Query string to forward verbatim (including the leading "?").
 */
export function resolveApiProxyUrl(
  path: string[] | undefined,
  backendUrl: string,
  search: string
): URL | null {
  const segments = path ?? []
  if (segments.some(isUnsafeSegment)) {
    return null
  }

  const pathname = segments.length > 0 ? `/api/${segments.join('/')}` : '/api'
  const url = new URL(pathname, backendUrl)

  // Backstop: after URL normalization the path must stay under /api/ with no
  // residual traversal or encoded separator the backend could still decode.
  const escapesApi = url.pathname !== '/api' && !url.pathname.startsWith('/api/')
  if (escapesApi || url.pathname.includes('..') || /%2f|%5c/i.test(url.pathname)) {
    return null
  }

  url.search = search
  return url
}
