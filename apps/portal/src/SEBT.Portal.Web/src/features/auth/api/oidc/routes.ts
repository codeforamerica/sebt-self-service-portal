/**
 * Path used when OIDC `/callback` cannot complete (IdP error redirect, missing `code`/`state`,
 * or token exchange / complete-login failure). Sends users to id-proofing off-boarding with a
 * dedicated `reason` query value. Applies whenever this callback route is used, not only for CO.
 */
export const OIDC_CALLBACK_ERROR_OFF_BOARDING =
  '/login/id-proofing/off-boarding?reason=oidcCallbackError'
