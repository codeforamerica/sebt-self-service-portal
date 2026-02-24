# Frontend Tech Debt Tracker

Tracks remediation of tech debt identified in the `src/SEBT.Portal.Web/` frontend.

## 1. Hardcoded Strings (i18n Gaps)

**Status**: Complete (blocked items documented below)

Replaced hardcoded English text in error pages, 404 page, and auth forms with i18n translation keys. Added keys to locale JSON files for both states (DC, CO) and both languages (en, es).

### Completed

- [x] Added `common` namespace keys: `errorSomethingWentWrong`, `errorUnexpectedBody`, `errorTryAgain`, `errorId`, `errorSessionExpired`, `errorSessionExpiredBody`, `errorPageBody`, `errorLogInAgain`, `pageNotFound`, `pageNotFoundBody`, `returnToHome`
- [x] Added `login` namespace keys: `errorUnexpected`, `codeSentSuccess`
- [x] Updated `src/app/error.tsx` to use `useTranslation`
- [x] Updated `src/app/(authenticated)/error.tsx` to use `useTranslation`
- [x] Updated `src/app/not-found.tsx` to use `getTranslations`
- [x] Updated `src/features/auth/components/login/LoginForm.tsx`
- [x] Updated `src/features/auth/components/verify/VerifyOtpForm.tsx`

### Remaining TODOs (Blocked)

- [ ] **TODO**: Spanish translations need professional review — currently using English text as placeholder in `es/dc/common.json`, `es/co/common.json`, `es/dc/login.json`, `es/co/login.json`
- [ ] **TODO**: `src/app/(public)/login/COLoginPage.tsx` — `login.title` is empty in CO locale, blocked on co.csv fix (clobbered by S8 OTP row). `common.logIn` and `common.logInEsp` exist in `en/co/common.json` but Spanish DC locale has column-shifted values.
- [ ] **TODO**: `src/components/layout/Footer.tsx` (COFooter) — `copyright`, `transparencyOnline`, `generalNotices` keys need to be added to co.csv by state partner
- [ ] **TODO**: `src/app/error.tsx` and `src/app/(authenticated)/error.tsx` — integrate error monitoring service (Sentry, DataDog, etc.) to replace `console.error`
- [ ] **TODO**: `src/features/household/components/ApplicationsSection/ApplicationsSection.tsx` — Status display text for Denied, Pending, Under Review, Cancelled needs CSV keys
- [ ] **TODO**: `src/features/household/components/CardStatusTimeline/` — Status aria-label and sr-only text keys missing
- [ ] **TODO**: `src/features/household/components/EbtEdgeSection/` — Section heading key missing
- [ ] **TODO**: `src/features/household/components/ActionButtons/` — nav aria-label key missing
- [ ] **TODO**: `src/features/household/components/DashboardContent/` — Error heading/description keys to be added to CSV
- [ ] **TODO**: `src/components/layout/HelpSection.tsx` — 6 CO-specific keys pending (helpDeskTitle, helpDeskBody, helpDeskEmail, accessibilityTitle, accessibilityBody, digitalAccessibilityStatement)
- [ ] **TODO**: `src/features/household/components/ChildCard/` — Card number suffix key pending

---

## 2. Dead Exports Cleanup

**Status**: Complete

Removed internal implementation details that were unnecessarily exported through barrel files, and removed unused devDependencies.

### Completed

- [x] Removed `DesktopLanguageSelector`, `MobileLanguageSelector` from `src/components/layout/index.ts`
- [x] Removed `DesktopLanguageSelector`, `MobileLanguageSelector` from `src/components/layout/LanguageSelector/index.ts`
- [x] Removed internal household component exports (`ActionButtons`, `ApplicationsSection`, `CardStatusTimeline`, `DashboardSkeleton`, `EbtEdgeSection`, `UserProfileCard`) from `src/features/household/components/index.ts`
- [x] Removed `@vitejs/plugin-react` devDependency from `package.json`

### Notes

- Kept all Zod schema exports, type exports, and hook exports — these are public API surface
- Kept `sass-embedded` and `sass-loader` — used in `next.config.ts` but knip can't detect
- Test fixture factories kept — actively used in test files

---

## 3. i18n Static Import Scaling

**Status**: Complete

Refactored the i18n system to use an auto-generated resource registry instead of 100+ manual static imports across `i18n.ts` and `translations.ts`.

### Completed

- [x] Enhanced `content/scripts/generate-locales.js` to generate `src/lib/generated-locale-resources.ts`
- [x] Refactored `src/lib/i18n.ts` to import from generated resource file (removed 60+ manual imports)
- [x] Refactored `src/lib/translations.ts` to import from generated resource file (removed 30+ manual imports)
- [x] Generated file auto-discovers all locale JSON files (77 imports, 2 states, 20 namespaces)

### Benefits

- Adding a new namespace: just create the JSON file and run `pnpm copy:generate`
- Adding a new state: just create the locale directory and run `pnpm copy:generate`
- Adding a new language: just create the locale directory and run `pnpm copy:generate`
- Zero manual import maintenance — script runs as part of `predev`, `prebuild`, and `pretest`

---

## 4. Prop Drilling in Household Feature

**Status**: Complete

Eliminated prop drilling by having child components call `useHouseholdData()` (via `useRequiredHouseholdData()`) directly instead of receiving the full `HouseholdData` object as props.

### Completed

- [x] Created `src/features/household/api/useRequiredHouseholdData.ts`
- [x] Updated `DashboardContent.tsx` — removed data prop passing
- [x] Updated `UserProfileCard.tsx` — uses hook instead of props
- [x] Updated `HouseholdSummary.tsx` — uses hook instead of props
- [x] Updated `EnrolledChildren.tsx` — uses hook instead of props
- [x] Updated `ApplicationsSection.tsx` — uses hook instead of props
- [x] Updated all affected component tests (mocking hook instead of passing props)

### Design Decision

Used TanStack Query cache deduplication rather than React Context. Multiple components calling the same query hook share cached data with zero extra network requests. `DashboardContent` still handles loading/error state gating.

---

## Verification

All checks pass after remediation:

- **TypeScript**: No type errors
- **ESLint**: 0 errors (6 pre-existing warnings unrelated to changes)
- **Tests**: 28 files, 288 tests — all passing
- **Build**: Next.js build succeeds, all pages generate correctly
