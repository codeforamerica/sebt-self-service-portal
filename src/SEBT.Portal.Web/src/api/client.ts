import type { ZodType } from 'zod'

import type { ApiErrorResponse } from './schemas'

const API_ROUTE_PREFIX = '/api'
const DEFAULT_TIMEOUT_MS = 30000

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public data?: ApiErrorResponse
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
 */
export async function apiFetch<T>(endpoint: string, options: ApiFetchOptions<T> = {}): Promise<T> {
  const { body, headers, method = 'GET', timeout = DEFAULT_TIMEOUT_MS, schema } = options

  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), timeout)

  let response: Response
  try {
    response = await fetch(`${API_ROUTE_PREFIX}${endpoint}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...headers
      },
      body: body !== undefined ? JSON.stringify(body) : null,
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
    return undefined as T
  }

  let data: T | ApiErrorResponse | undefined
  const contentType = response.headers.get('content-type')
  if (contentType?.includes('application/json')) {
    data = (await response.json()) as T | ApiErrorResponse
  }

  if (!response.ok) {
    const errorData = data as ApiErrorResponse | undefined
    const message =
      errorData?.error ?? errorData?.message ?? `Request failed with status ${response.status}`
    throw new ApiError(message, response.status, errorData)
  }

  if (schema && data !== undefined) {
    const result = schema.safeParse(data)
    if (!result.success) {
      throw new ApiValidationError(
        `Response validation failed for ${endpoint}`,
        result.error.issues
      )
    }
    return result.data
  }

  return data as T
}
