/**
 * Per-route HTTP response headers applied via Next.js `headers()` config.
 *
 * `/login` is set to `Cache-Control: no-store` so that browsers do not store
 * the page in the back/forward cache (BFCache). The login flow does a hard
 * external navigation to the state IdP (PingOne) via `window.location.href`;
 * without `no-store`, a back-navigation from the IdP restores `/login` from
 * BFCache and React never re-hydrates, leaving onClick handlers detached and
 * the sign-in buttons unresponsive until the user manually refreshes.
 *
 * MDN: https://developer.mozilla.org/en-US/docs/Web/API/Window/pageshow_event#bfcache
 */
export async function getRouteHeaders() {
  return [
    {
      source: '/login',
      headers: [{ key: 'Cache-Control', value: 'no-store' }]
    }
  ]
}
