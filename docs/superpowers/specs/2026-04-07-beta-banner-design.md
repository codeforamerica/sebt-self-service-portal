# Beta Banner — Design Spec

## Summary

Add a site-wide beta banner that displays at the top of every page when enabled by a feature flag. The banner uses the USWDS info alert pattern and is localized via i18next.

## Requirements

- Banner appears on every page (public and authenticated) when `enable_beta_banner` is `true`
- Banner is hidden when the flag is `false` (the default)
- Text is localized — English fallback: *"This site is currently in beta. Some features may be incomplete or missing."*
- Visual style matches Figma: USWDS info alert (light cyan background, teal left border, info icon, body text only — no heading)

## Component

**File:** `src/SEBT.Portal.Web/src/components/BetaBanner.tsx`

A `'use client'` component that:

1. Reads the `enable_beta_banner` flag via `useFeatureFlag('enable_beta_banner')`
2. Returns `null` when the flag is off
3. Renders `<Alert variant="info">` from `@sebt/design-system` with a localized `t('betaBannerText')` body
4. No heading — body text only, matching the Figma

## Placement

In the root layout (`src/SEBT.Portal.Web/src/app/layout.tsx`), between the `<div id="site-alerts" />` and `<Header />`:

```tsx
<div id="site-alerts" />
<BetaBanner />
<Header state={state} />
```

This positions the banner above the state header on every page, matching the Figma mockup. The component sits inside `FeatureFlagsProvider` and `I18nProvider`, so both hooks are available.

## Feature Flag

- **Flag name:** `enable_beta_banner`
- **Default:** `false` in `appsettings.json`
- **CO opt-in:** `true` in `appsettings.co.json`
- Uses the existing `Microsoft.FeatureManagement` → `GET /api/features` → `useFeatureFlag` pipeline

## Localization

- **i18n key:** `betaBannerText`
- **English fallback:** `"This site is currently in beta. Some features may be incomplete or missing."`
- **Content gap:** The key must be added to the source Google Sheet and the CSV re-exported. Until then, the `t()` fallback string provides the English text.

## Files Touched

| Action | File | Change |
|--------|------|--------|
| NEW | `src/SEBT.Portal.Web/src/components/BetaBanner.tsx` | Component implementation |
| NEW | `src/SEBT.Portal.Web/src/components/BetaBanner.test.tsx` | Unit tests |
| EDIT | `src/SEBT.Portal.Web/src/app/layout.tsx` | Import and render `<BetaBanner />` |
| EDIT | `src/SEBT.Portal.Api/appsettings.json` | Add `"enable_beta_banner": false` to `FeatureManagement` |
| EDIT | `src/SEBT.Portal.Api/appsettings.co.json` | Add `"enable_beta_banner": true` to `FeatureManagement` |

## Testing

- **Unit test:** `BetaBanner.test.tsx`
  - Renders alert text when `enable_beta_banner` is `true`
  - Renders nothing when `enable_beta_banner` is `false`
  - Uses the `info` alert variant
  - Text comes from the `betaBannerText` i18n key

## Out of Scope

- Dismissibility (the banner is always visible when the flag is on)
- Banner text configurability beyond i18n (all states share the same message keys)
- DC-specific opt-in (DC can enable the flag when ready)
