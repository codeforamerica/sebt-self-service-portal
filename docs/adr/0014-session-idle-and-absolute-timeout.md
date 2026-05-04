# ADR 0014: Session Idle and Absolute Timeout

## Status

Accepted

## Context

The portal session is an HttpOnly cookie carrying a JWT, renewed by `POST /api/auth/refresh`. A `TokenRefresher` component was calling refresh on a fixed 10-minute interval whenever an authenticated tab was open. The interval had no notion of user activity, so a tab left open kept the session alive indefinitely — bypassing idle timeout. Reported as a security issue (#267).

The server JWT also had no absolute lifetime — refresh requests were always honored.

## Decision

Implement two complementary timeouts, aligned with [OWASP Session Management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html) and [NIST SP 800-63B §7.1](https://pages.nist.gov/800-63-3/sp800-63b.html) (IAL2: ≤30 min idle, ≤12 hr absolute).

**Idle (sliding) timeout — `JwtSettings.ExpirationMinutes`, default 15 min.**
The session cookie expires this long after the most recent renewal. Renewal is now activity-gated: `TokenRefresher` schedules a single `setTimeout` keyed off the server-returned `expiresAt`, and at fire time only refreshes if `now − lastActivityAt < idleThreshold`. DOM events (`mousedown`, `keydown`, `touchstart`, `scroll`, `visibilitychange`) update `lastActivityAt` via a 1-second-throttled listener. Idle users stop refreshing; the cookie lapses and the next API call's 401 redirects to `/login`.

**Absolute timeout — `JwtSettings.AbsoluteExpirationMinutes`, default 60 min.**
Stamped once at first login as the standard OIDC `auth_time` claim. Refresh forwards the same `auth_time` forward — never re-stamps. `RefreshTokenCommandHandler` rejects with `Result<string>.Unauthorized` when `now − auth_time ≥ AbsoluteExpirationMinutes`. The controller maps this to 401 + `AuthCookies.ClearAuthCookie`. A configuration validator enforces `AbsoluteExpirationMinutes ≥ ExpirationMinutes`.

`/api/auth/status` exposes both `expiresAt` and `absoluteExpiresAt` so the SPA can schedule and halt refreshes without round-tripping for the JWT contents.

The activity-tracking and refresh-scheduling logic is hand-rolled (~3 small modules, ~80 LOC total). A library (`react-idle-timer`) was evaluated and rejected: 3-year release gap, 40 untriaged issues, no React 19 verification, and we would use <10% of its surface (no prompt, no leader election, no cross-tab sync — per-tab independence is intentional).

## Consequences

- Idle sessions expire after 15 min of no activity. Active users renew silently.
- All sessions terminate at 60 min regardless of activity. Returning users re-authenticate.
- Tokens minted before this change lack `auth_time` and are rejected on refresh — a one-time forced re-login window when this deploys (acceptable; ≤15 min cleared by the new idle window).
- Per-tab activity tracking: each tab schedules its own refresh, so activity in one tab does not silently extend another idle tab's session.
- Defaults are tighter than NIST IAL2 ceilings; can be relaxed via configuration without code changes.
