import {
  reportOidcCallbackFailure,
  type ReportOidcCallbackFailureParams
} from './reportCallbackFailure'
import { OIDC_CALLBACK_ERROR_OFF_BOARDING } from './routes'

/**
 * Reports OIDC callback failure to the API (when provided) and redirects to off-boarding.
 */
export function redirectToOidcOffBoarding(
  router: { replace: (href: string) => void },
  report?: ReportOidcCallbackFailureParams
): void {
  if (report) {
    reportOidcCallbackFailure(report)
  }
  router.replace(OIDC_CALLBACK_ERROR_OFF_BOARDING)
}
