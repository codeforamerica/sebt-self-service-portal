/** Same-origin API prefix used by <c>apiFetch</c>. */
const API_ROUTE_PREFIX = '/api'

/**
 * Reasons accepted by POST /api/auth/oidc/report-failure.
 * API exchange/complete-login failures are logged on the server only.
 */
export type OidcCallbackFailureReason = 'idp_redirect' | 'missing_params'

export interface ReportOidcCallbackFailureParams {
  reason: OidcCallbackFailureReason
  idpError?: string | undefined
  idpErrorDescription?: string | undefined
  hasCode?: boolean | undefined
  hasState?: boolean | undefined
}

/**
 * Fire-and-forget server log for OIDC callback failures that redirect to off-boarding.
 * Uses <c>keepalive</c> so the request can finish after <c>router.replace</c> navigates away.
 * Swallows errors so logging never blocks the user redirect.
 */
export function reportOidcCallbackFailure(params: ReportOidcCallbackFailureParams): void {
  void fetch(`${API_ROUTE_PREFIX}/auth/oidc/report-failure`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(params),
    credentials: 'same-origin',
    keepalive: true
  }).catch(() => {
    // Logging must not affect the off-boarding redirect.
  })
}
