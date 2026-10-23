# 24. Render localized content on the server, behind locale-prefixed routes

Date: 2026-09-23

## Status

Proposed. Amends the language-selection and message-loading decisions in [ADR 0006](0006-i18n-implementation.md). The choice of `react-i18next` over `next-intl`, and the Google Sheet → CSV → JSON content pipeline, both stand.

## Context

ADR 0006 chose a client-side i18n architecture, and it shipped as designed. The language lives in `localStorage` under `i18nextLng`; `packages/design-system/src/providers/I18nProvider.tsx` reads it in a `useEffect` and calls `i18n.changeLanguage()`. A `?lang=` query parameter seeds that value once, but the language selector never writes it — `LanguageSelector.tsx` calls `changeLanguage()` and nothing else. No cookie is set, and nothing reads `Accept-Language` anywhere in the repository.

The consequence is that **the server does not know what language to render.** `app/layout.tsx` hardcodes `lang="en"` on the `<html>` element, every component calling `useTranslation` sits behind a `'use client'` boundary, and so every server-rendered response is English. The correct language appears only after hydration, as a repaint. This is visible enough that the Amharic end-to-end spec allows a 20-second timeout for the flip, and it is not merely cosmetic: a visitor on a slow connection, or with JavaScript disabled, reads English regardless of preference, and a crawler only ever sees English.

Three things have changed since ADR 0006 that make this worth revisiting now.

**The state is resolved per request, but the language is not.** [ADR 0023](0023-runtime-client-config.md) moved `STATE` to request time and established the pattern for handing a server-resolved value to client components: the server stamps `<html data-state>`, and `getState()` reads it back. Language is the one remaining dimension of the UI that the server still cannot resolve, and the plumbing for fixing it already exists in a proven form.

**One artifact now serves every state, so every state's messages ship to every browser.** `src/lib/generated-locale-resources.ts` contains 118 static `import` statements — 197 KB of raw JSON across `en|es|am` × `dc|co` — and reaches the client through a `'use client'` boundary. `initI18n` selects one state's slice at runtime, but the other state's tree is still in the bundle. The comment in `packages/design-system/src/lib/i18n.ts` claiming "build-time state isolation ensures only one state's translations are bundled" is no longer accurate.

**Amharic is DC-only.** `packages/design-system/src/lib/state.ts` gives DC `['en', 'es', 'am']` and CO `['en', 'es']`. Any routing scheme has to validate a locale against the state being served, not against a global list.

## Decision

### Routing structure

Move the application under a `[lang]` root segment — `app/[lang]/layout.tsx` becomes the root layout, with the existing `(public)` and `(authenticated)` route groups nested beneath it. This is the App Router's documented internationalization pattern, and on Next 16.3.4 it unlocks `next/root-params` (added in 16.3.0): `await lang()` is readable from any Server Component or server-side utility without threading `params` through every layer.

Supported locales are validated per state. `/am` is a valid path on DC and must not resolve on CO.

### Locale detection order

Resolved in `proxy.ts`, first match wins:

1. **Path prefix** — `/es/dashboard` is unambiguous and shareable.
2. **`NEXT_LOCALE` cookie** — set only when a visitor explicitly picks a language.
3. **`Accept-Language`**, negotiated against the state's supported set.
4. **`en`** as the fallback.

Region-qualified codes are normalized before matching (`en-US` → `en`), since the content pipeline is keyed by language alone.

A request without a locale prefix is redirected to the resolved locale (`/dashboard` → `/es/dashboard`). A request naming a locale the state does not support is redirected to the default rather than 404'd, so a DC link to `/am/...` forwarded to a CO user degrades to readable content instead of an error page.

### Message storage and loading

**Keep the generated filesystem JSON. Do not introduce a remote message backend, and do not build a cache in front of the loader.**

Server-side loading is a dictionary of dynamic imports keyed by locale, which is the framework's documented pattern. Node's module cache already memoises each imported namespace for the process lifetime, with correct invalidation semantics on redeploy. An explicit in-memory LRU would be a second cache in front of a cache, and a filesystem-reading loader would be actively fragile: `output: 'standalone'` copies only the files the build traced, and these JSON files are traced precisely *because* they are imported. Reading them through `fs` at runtime would work in development and fail in the container.

No shared cache (Redis or otherwise) is required. Messages are per-deployment static content, identical on every instance.

### Metadata localization

`generateMetadata` in the root layout is the only metadata definition in the application, and it is currently language-unaware — English-only title and description from `state.ts`, and a hardcoded `openGraph.locale: 'en_US'`. Once the locale is a root parameter it becomes the input to all three, plus `alternates.languages`, which Next renders as `<link rel="alternate" hreflang>`. `sitemap.ts` gains per-locale entries for the seven public routes.

`public/robots.txt` is a static file that hardcodes `Sitemap: https://sebt.dc.gov/sitemap.xml`. It is not parameterized by state, so it is already wrong for Colorado and would need to become a generated route alongside the per-locale sitemap work.

This is the one part of the work that is blocked on content rather than engineering: localized titles and descriptions do not exist in the state CSVs today and must be authored in the Google Sheet first.

### Caching and CDN behavior

Nothing needs to change, and the write-up should say so plainly rather than inventing work. Every route is already dynamically rendered — the root layout awaits `headers()`, which taints the whole tree — so Next emits `private, no-cache, no-store, max-age=0, must-revalidate`, and the portal's CloudFront distribution uses `Managed-CachingDisabled`. Per-locale HTML caching is not a problem to solve because HTML is not cached at all.

Two constraints apply if that ever changes. Proxy is documented to belong *in front of* the CDN; here CloudFront sits in front of the origin, so any route whose response depends on a proxy decision must bypass the cache. And locale-in-path removes one axis of cache variance but not the others: App Router responses still vary on `rsc` and `next-router-*` headers, which Next discriminates with the `_rsc` search parameter.

### Phasing

The value is separable from the URLs, and the first phase carries most of it:

1. **Make the server language-aware** — cookie plus `Accept-Language`, `<html lang>` rendered correctly, the client i18n instance seeded from the server's choice. This ends the English flash without moving a single route.
2. **Introduce locale-prefixed routes** — the `[lang]` restructure and proxy redirects.
3. **Localize metadata** — titles, descriptions, Open Graph, hreflang, sitemap.

## Alternatives considered

**Proxy rewrite instead of a `[lang]` segment.** `/es/dashboard` rewrites internally to `/dashboard` with the locale passed in a request header, which the root layout already reads for the CSP nonce. This keeps the entire file tree, every test path, and every E2E selector intact — a materially smaller diff. It gives up `next/root-params` (the locale is not a real route segment), typed `LayoutProps<'/[lang]'>`, and any future `generateStaticParams` over locales. Worth choosing if the restructure's blast radius proves unacceptable during phase 2; the proxy-side detection logic is identical either way.

**Cookie only, no URL change.** This is phase 1, not a rejected option. It is sufficient for the flash and for `<html lang>`, and insufficient for shareable links and hreflang.

**`serverLoadMessages(locale, namespaces)` with an LRU cache and filesystem fallback.** Rejected for the reasons given above — it caches what Node already caches, and its fallback path breaks under `output: 'standalone'`. Recorded here because the spike brief asked for it specifically.

**Moving to `next-intl`.** Out of scope. ADR 0006's reasoning holds, and the gap being closed is *where rendering happens*, not which library formats the strings.

## Deviations from the generic SSR draft

The spike brief was derived from a semi-generic SSR/i18n draft (`sebt_ssr.md`, attached to the spike ticket). That draft is explicitly framework-generic — it uses French as its example locale, `pages/[locale]/[...slug].tsx` for routing, and `getServerSideProps` for data loading — so several of its concrete recommendations do not survive contact with this codebase. The differences are listed here so an implementer working from the draft, or from the tickets generated out of it, knows which parts were deliberately not followed.

| Draft says | This ADR says | Why |
| --- | --- | --- |
| `pages/[locale]/[...slug].tsx`, `getServerSideProps` | `app/[lang]/`, `next/root-params` | The portal is App Router; `getServerSideProps` does not exist there. |
| `/locales/{locale}/{namespace}.json` | `content/locales/{lang}/{state}/{namespace}.json` | Content is per language **and per state**. The draft has no multi-state concept; flattening it would merge DC and CO copy. |
| One `supportedLocales` list | Supported locales resolved per state | DC serves `en/es/am`, CO serves `en/es`. A single list lets `/am` resolve on CO with no content behind it. |
| In-memory LRU, TTL of 5–60 minutes, optional Redis | Node's module cache; no LRU, no TTL, no Redis | Messages are statically imported and change only on redeploy, so a TTL expires nothing and a second cache adds no hits. |
| Filesystem read as the loader fallback | Imports only | `output: 'standalone'` ships only traced files; an `fs` read would work in development and fail in the container. |
| Optional remote translations backend | Keep the CSV → JSON pipeline | Content is authored by the content team in a Google Sheet, per ADR 0006 and ADR 0009. A remote backend would bypass that workflow, not serve it. |
| CDN-cached HTML per locale, in the title decision | No HTML caching | Every route is dynamic and authenticated pages render household PII. See the caching section; this is the most consequential deviation. |
| "Translation edits must propagate without full rebuilds" | Recorded as an open question | Stated as a requirement in the draft, but it is not true of this app today, and making it true is a larger change than locale routing. |
| RTL visual-regression testing | Not in scope | `en`, `es` and `am` are all left-to-right. Worth revisiting only if an RTL language is added. |

Two parts of the draft are adopted as written: normalizing locale codes (`en-US` → `en`) before matching, and having the language switcher change the URL rather than only a cookie. Its "no FOUC" goal is the same defect this ADR calls the English flash, and phase 1 delivers it without any of the routing work.

## Prototype

Annotated and illustrative — it names the real files and the real constraints, but is not drop-in code.

### Locale resolution in `proxy.ts`

```ts
// Placement matters. The /api/enrollment CORS branch (proxy.ts:16-37) returns early and must
// keep doing so; locale handling goes after it and before the CSP block, so a request that is
// about to be redirected does not pay for nonce generation and policy construction.

const SUPPORTED = supportedLanguagesFor(process.env.STATE)   // state.ts: DC en|es|am, CO en|es
const DEFAULT_LOCALE = 'en'

function localeFromPath(pathname: string) {
  const [, first] = pathname.split('/')
  return SUPPORTED.includes(first) ? first : null
}

function resolveLocale(request: NextRequest) {
  const cookie = request.cookies.get('NEXT_LOCALE')?.value
  if (cookie && SUPPORTED.includes(cookie)) return cookie
  return negotiate(request.headers.get('accept-language'), SUPPORTED) ?? DEFAULT_LOCALE
}

// API routes are matched by the current matcher and must never be redirected.
if (!pathname.startsWith('/api/')) {
  const inPath = localeFromPath(pathname)

  if (!inPath) {
    // No locale, or one this state does not support: send them to a locale that exists.
    request.nextUrl.pathname = `/${resolveLocale(request)}${pathname}`
    return NextResponse.redirect(request.nextUrl)
  }
}
```

Three details about the existing configuration that this has to respect:

- The matcher at `proxy.ts:157-174` carries only the `missing: [next-router-prefetch, purpose=prefetch]` half of the documented negative-matching pattern, so **prefetch requests skip the proxy entirely** and receive no locale handling. Next also strips `rsc` and `next-router-*` from `request.headers` inside Proxy, so this cannot be detected from within the function — it has to be fixed in the matcher.
- `headers` and `redirects` from `next.config.ts` execute *before* Proxy. The `/login` `no-store` rule in `route-headers.ts` and the `/index.html` redirect both need locale-aware patterns.
- Proxy runs on the Node.js runtime by default in Next 16, so nothing here is constrained by the edge runtime.

### Server-side message loading

```ts
// The generated registry (src/lib/generated-locale-resources.ts) already maps
// state → language → namespace. On the server, read the locale from the root param
// rather than threading it through every caller.
import { lang } from 'next/root-params'

const dictionaries = {
  en: () => import('@/content/locales/en'),
  es: () => import('@/content/locales/es'),
  am: () => import('@/content/locales/am')
}

export async function getMessages(namespaces: Namespace[]) {
  const locale = await lang()
  if (!isSupported(locale)) notFound()
  // No LRU. Node's module cache holds each import for the process lifetime.
  return pick(await dictionaries[locale](), namespaces)
}
```

`next/root-params` is Server Components only — not Client Components, Server Actions, or Route Handlers. Since nearly all of the UI is client-side today, phase 1 passes the resolved locale into the existing `initI18n` call so the client instance starts in the right language instead of correcting itself after hydration.

### Localized layout and metadata

```tsx
// app/[lang]/layout.tsx
import { lang } from 'next/root-params'

export async function generateMetadata(): Promise<Metadata> {
  const locale = await lang()
  const t = await getMessages(['common'])

  return {
    title: { default: t('portalTitle'), template: `%s | ${t('siteName')}` },
    description: t('portalDescription'),
    openGraph: { locale: ogLocale(locale) },        // not the hardcoded 'en_US'
    alternates: {
      canonical: `${baseUrl}/${locale}${pathname}`,
      languages: Object.fromEntries(
        SUPPORTED.map((l) => [l, `${baseUrl}/${l}${pathname}`])
      )
    }
  }
}

export default async function RootLayout(props: LayoutProps<'/[lang]'>) {
  return (
    <html lang={await lang()} data-state={getState()}>
      ...
    </html>
  )
}
```

## Consequences

- Non-English visitors receive correctly localized HTML on first paint. The English flash, and the 20-second allowance in the Amharic E2E spec, both go away.
- `<html lang>` is correct in the served response rather than corrected by script, which is what assistive technology reads.
- Language becomes linkable. Outreach can publish `/es/faqs`, and hreflang becomes expressible for the seven public routes.
- **The tests that encode the current behavior must be rewritten, not merely adjusted.** `I18nProvider.test.tsx` has eight tests locking the `?lang=` > `localStorage` precedence; both i18n E2E specs seed `localStorage['i18nextLng']` and assert `html[lang]`; `test-setup.ts` initializes the real i18next instance for every portal unit test.
- Existing `?lang=` links in the wild, and the OIDC login flow that forwards `language=` to PingOne and re-reads `localStorage` on return (`COLoginPage.tsx`), both need a compatibility path.
- The `[lang]` restructure moves every page, layout, and route group, and touches every test and E2E path that hardcodes a URL. This is the largest single cost, and the rewrite alternative above exists to avoid it.
- **Analytics accuracy improves as a side effect.** `DataLayerProvider` derives both `page.language` and `page.locale` from `document.documentElement.lang`. Because that attribute is `en` until the post-hydration flip, language is currently captured from whatever the DOM says at that instant — so non-English sessions can be recorded as English depending on timing. Rendering the attribute correctly on the server fixes the measurement at its source. Worth checking separately: the derived locale is built as `` `${lang}_US` ``, which yields `am_US` and `es_US`.
- Bundle size does not improve on its own. Messages ship to the client while components remain client-side; removing that weight is an RSC conversion, which is a separate and much larger effort. The existing budgets in `.size-limit.json` (150 KB first load, 500 KB total) have no per-locale dimension, so bundled message growth is invisible to them until it crosses the aggregate limit.
- `i18next` is initialized with `react: { useSuspense: false }`. Any later move toward server-rendered messages or streaming should revisit that, since the setting exists to make a client-only instance render synchronously.
- The enrollment checker cannot share any of this. It deploys as `output: 'export'`, and Proxy is explicitly unsupported for static exports, so locale routing there needs its own mechanism.

## Follow-up work

Four implementation tickets (A–D) were drafted on the spike ticket ahead of this write-up, generated from the same generic draft. They map onto this plan as: A → 2, B → 3, C → 4, D → 6. Two steps below have no counterpart there — phase 1, which delivers the main user-visible win on its own, and the test migration, which is not optional given how thoroughly the current mechanism is encoded in tests. Where a drafted ticket's acceptance criteria conflict with this ADR, the deviations table above gives the reason.

In order:

1. **Resolve the language on the server** — cookie and `Accept-Language` negotiation in `proxy.ts`, `<html lang>` from the resolved value, client i18n seeded from it. Delivers the flash fix on its own.
2. **Locale-prefixed routing** — the `[lang]` restructure (or the rewrite alternative), redirects for missing and unsupported locales, language selector writing the URL and cookie, and a compatibility path for `?lang=` and the OIDC return.
3. **Server-side message loading** — the dictionary loader above, wired for the components that are already server-rendered.
4. **Localized metadata and hreflang** — title, description, Open Graph, `alternates.languages`, per-locale sitemap entries, and a state-aware `robots.txt` to replace the hardcoded DC sitemap URL. Blocked on new content rows in the Google Sheet.
5. **Test and E2E migration** — rewrite the provider precedence tests and re-seed both i18n E2E specs against the new mechanism.
6. **CDN and rollout** — only if HTML caching is ever enabled: proxy-dependent routes bypass the cache, `_rsc` stays in the cache key, and per-locale traffic is monitored after rollout.

## Open questions

Answerable from the code, recorded so the follow-up tickets do not re-litigate them:

- **Runtime model.** Both production paths run the same `next build` standalone Node server — an Alpine container on Fargate, and the same `server.js` launched by IIS `httpPlatformHandler` on the DC host. Proxy behaves identically in both. The filesystem is readable in both, but nothing here needs to read it.
- **Prerequisites.** None. Next 16.3.4 already provides everything, including `next/root-params`. No upgrade, no Redis, no CDN reconfiguration.
- **Analytics.** The dimension already exists — `DataLayerProvider` sets `page.language` and `page.locale` from `<html lang>`, and `web-vitals.ts` snapshots `language`. Any change to how the locale is resolved must keep those populated; see Consequences for why the current values are probably under-reporting non-English sessions.
- **Initial locales.** `en`/`es` for both states, plus `am` for DC, matching `state.ts` today.

Needing a decision from product, content, or infrastructure:

- **Translation freshness.** Copy changes currently require a CSV re-export, a rebuild, and a redeploy, because the JSON is generated at build time and bundled. If near-real-time content updates are a requirement, that is a larger change than locale routing and deserves its own ADR.
- **Backward compatibility and SEO guarantees.** How long must `?lang=` URLs keep working, and are any of them published externally?
- **Locale-specific policy constraints.** Whether any state has requirements beyond translation — legal copy, date and currency formatting, or right-to-left support for a future language.
- **Ownership.** Who maintains the i18n infrastructure and the translation workflow, and whether the enrollment checker is expected to follow the portal's scheme.

## References

- [ADR 0006](0006-i18n-implementation.md) — the client-side i18n decision this amends.
- [ADR 0009](0009-locale-section-filtering.md) — CSV section filtering in the generator.
- [ADR 0023](0023-runtime-client-config.md) — per-request `STATE` and the `data-state` handoff pattern.
- `packages/design-system/src/providers/I18nProvider.tsx` — the post-hydration language selection being replaced.
- `packages/design-system/src/lib/state.ts` — per-state supported languages.
- `apps/portal/src/SEBT.Portal.Web/src/proxy.ts` — where locale resolution lands.
- `apps/portal/src/SEBT.Portal.Web/src/app/layout.tsx` — hardcoded `lang="en"` and the only `generateMetadata`.
