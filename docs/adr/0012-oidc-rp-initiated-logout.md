# ADR 0012: OIDC RP-Initiated Logout

## Status

Accepted

## Context

The portal supports OIDC login via PingOne for Colorado (CO). When a user logs out, the portal clears the local session cookie but leaves the PingOne SSO session alive. If the user (or the next person on a shared computer) clicks "Login" again, PingOne silently re-authenticates using the active SSO session — no credential challenge.

We send `prompt=login` and `max_age=0` in the authorize request, and PingOne does issue a fresh `auth_time` claim, but it considers session re-validation as satisfying re-authentication. There is no PingOne parameter (standard or proprietary) that forces credential re-entry from the authorize request alone.

This is a security concern for shared/public computers (e.g., library kiosks) where logout must mean the next user cannot access the previous session.

## Decision

Implement RP-Initiated Logout (OIDC RP-Initiated Logout 1.0). When a user signs out:

1. The portal clears the local session cookie.
2. The server redirects the browser to PingOne's `end_session_endpoint` with `client_id` and `post_logout_redirect_uri`.
3. PingOne terminates the SSO session and redirects back to the portal's login page.

A single `GET /api/auth/logout` endpoint handles both OIDC (CO) and non-OIDC (DC) states. The server decides the redirect chain based on whether `Oidc:DiscoveryEndpoint` is configured. The frontend navigates to this endpoint via a standard anchor link — no `fetch()` or JavaScript-mediated redirect.

## Consequences

- Logout on OIDC states terminates both the portal session and the IdP session.
- Logout becomes a full-page navigation (browser redirect chain) instead of an async `fetch()` call. React state is cleared naturally by the page reload.
- DC (non-OIDC) behavior is unchanged — the endpoint redirects to `/login` after clearing the cookie.
- `post_logout_redirect_uri` must be registered with PingOne for each environment.
- The `auth_time` claim is logged (not enforced) as defense-in-depth observability for future IdP behavior changes.
