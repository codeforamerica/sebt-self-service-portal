// Browser-facing configuration for the enrollment checker, read by
// src/lib/client-config.ts. Loaded from <head> before the app bundle, so the
// values are available synchronously on first paint.
//
// This file ships as part of the static export but is meant to be REPLACED in
// the deployed bucket per environment — that is what lets one build artifact
// run anywhere. Leaving it empty falls back to the values baked in at build
// time, which is what local development and tests rely on.
//
// Every key is optional. Recognized keys:
//   apiBaseUrl, portalUrl, applicationUrl,
//   showSchoolField, checkerEnabled, botProtectionEnabled,
//   gaId, amplitudeApiKey, mixpanelToken, siteImproveId,
//   metaPixel, metaPixelAction, adentifiPixelLanding, adentifiPixelApplyNow
window.__CHECKER_CONFIG__ = {}
