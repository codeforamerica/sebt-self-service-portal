import type { ZodType } from 'zod'

import type { ApiErrorResponse } from './schemas'

const API_ROUTE_PREFIX = '/api'
const DEFAULT_TIMEOUT_MS = 30000

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public data?: ApiErrorResponse,
    /**
     * True when the 401 path triggered `window.location.replace('/login')`. Consumers
     * that render error UIs should treat this as a loading state — the page is
     * unmounting, and the user shouldn't briefly see an error before the redirect.
     */
    public readonly isRedirecting: boolean = false
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export class ApiTimeoutError extends Error {
  constructor(message = 'Request timed out') {
    super(message)
    this.name = 'ApiTimeoutError'
  }
}

export class ApiValidationError extends Error {
  constructor(
    message: string,
    public errors: unknown
  ) {
    super(message)
    this.name = 'ApiValidationError'
  }
}

interface ApiFetchOptions<T> {
  method?: string
  body?: unknown
  headers?: HeadersInit
  timeout?: number
  /** Optional Zod schema to validate the response data at runtime */
  schema?: ZodType<T>
}

/**
 * Fetch wrapper for API calls with timeout, error handling, and optional schema validation.
 * All requests are routed through Next.js API proxy (/api/*) which forwards to the backend.
 * Auth is carried by the browser's HttpOnly session cookie — no Authorization header is set here.
 */
export async function apiFetch<T>(endpoint: string, options: ApiFetchOptions<T> = {}): Promise<T> {
  const { body, headers, method = 'GET', timeout = DEFAULT_TIMEOUT_MS, schema } = options

  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), timeout)

  const requestHeaders: HeadersInit = {
    'Content-Type': 'application/json',
    ...headers
  }

  let resolvedEndpoint = endpoint
  if (method === 'GET') {
    // Append per-request UUID so edge caches (e.g. Cloudflare) can never
    // serve User A's authenticated response to User B. See ADR 0016.
    const url = new URL(endpoint, 'http://placeholder.invalid')
    url.searchParams.set('_', crypto.randomUUID())
    resolvedEndpoint = `${url.pathname}${url.search}`
  }

  let response: Response
  try {
    response = await fetch(`${API_ROUTE_PREFIX}${resolvedEndpoint}`, {
      method,
      headers: requestHeaders,
      body: body !== undefined ? JSON.stringify(body) : null,
      credentials: 'same-origin',
      signal: controller.signal
    })
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw new ApiTimeoutError(`Request to ${endpoint} timed out after ${timeout}ms`)
    }
    throw error
  } finally {
    clearTimeout(timeoutId)
  }

  if (response.status === 201 || response.status === 204) {
    // 201/204 responses have no body. Callers for these endpoints
    // should expect void or ignore the return value.
    return undefined as unknown as T
  }

  let data: T | ApiErrorResponse | undefined
  const contentType = response.headers.get('content-type')
  if (contentType?.includes('application/json')) {
    data = (await response.json()) as T | ApiErrorResponse
  }

  if (!response.ok) {
    // 401 from any controller means the session is invalid (bearer middleware rejected
    // the JWT — expired exp, missing/expired auth_time, etc). Redirect to /login on
    // every endpoint EXCEPT /auth/status, which is the bootstrap probe AuthContext uses
    // to detect "not logged in" without redirecting. Resource-level access denial (e.g.,
    // IAL gating) returns 403 with structured ProblemDetails — never 401 — so any 401
    // here is unambiguously session-level.
    const isBootstrapProbe = endpoint === '/auth/status'
    const isRedirecting =
      response.status === 401 && !isBootstrapProbe && typeof window !== 'undefined'
    if (isRedirecting) {
      window.location.replace('/login')
    }

    const errorData = data as ApiErrorResponse | undefined
    const message =
      errorData?.error ?? errorData?.message ?? `Request failed with status ${response.status}`
    throw new ApiError(message, response.status, errorData, isRedirecting)
  }

  if (schema && data !== undefined) {
    const result = schema.safeParse(data)
    if (!result.success) {
      console.error(`[apiFetch] Validation failed for ${endpoint}:`, result.error.issues)
      console.error(`[apiFetch] Raw response data:`, data)
      throw new ApiValidationError(
        `Response validation failed for ${endpoint}`,
        result.error.issues
      )
    }
    return result.data
  }

  return data as T
}
