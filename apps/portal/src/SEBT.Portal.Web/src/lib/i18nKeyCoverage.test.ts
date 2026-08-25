/**
 * Guard: every translation key the source asks for exists in the content it ships with.
 *
 * When a sheet row is set to `!N/A!`, the generator drops the key entirely
 * (`generate-locales.js`), and i18next then renders the key name where a
 * sentence belongs. That is how `logInDisclaimerBody2` reached the Colorado
 * sign-in page: the CSV changed, no code changed, and the page test kept passing
 * because it asserted against a hand-written copy of the locale file rather than
 * the file itself.
 *
 * This compares what the code asks for against what the pipeline produces, so a
 * content-only change that strands a key fails here instead of in the browser.
 *
 * A call with a hardcoded default (`t('key', 'English')`) is out of scope: it
 * renders English rather than the key. That is a different defect.
 */
import { describe, expect, it } from 'vitest'

import {
  APP_STATES,
  extractTranslationCalls,
  loadBundles,
  resolves,
  sourceRoots,
  staleExemptions,
  walkSource,
  type AppName,
  type TranslationCall
} from './i18nContentScan'

interface Exemption {
  /** `namespace:key`, matching the namespace the call site resolves to. */
  key: string
  /** States where the key is expected to be absent. */
  states: string[]
  reason: string
}

/**
 * Keys that are absent on purpose, each with the reason it is safe.
 *
 * Adding an entry here is a decision, not a formality: it asserts that no user
 * of that state can reach a screen where the key renders. If you cannot say why
 * a key is unreachable, it belongs in the content sheet instead.
 */
const EXEMPT: Exemption[] = [
  // Colorado has no identity-verification screens. The sheet marks every
  // `S8 - ID Proofing Optional ID Info` and `S8 - Off-boarding` row `!N/A!` for
  // CO, and no Colorado route reaches either screen: the code-entry sign-in that
  // leads there is DC-only, and CO's `household+view` requires only IAL1, so the
  // 403 that would redirect a user into the form never fires.
  { key: 'idProofing:title', states: ['co'], reason: 'CO has no identity-verification screen' },
  { key: 'idProofing:body', states: ['co'], reason: 'CO has no identity-verification screen' },
  { key: 'idProofing:labelDob', states: ['co'], reason: 'CO has no identity-verification screen' },
  { key: 'idProofing:helperDob', states: ['co'], reason: 'CO has no identity-verification screen' },
  { key: 'idProofing:labelId', states: ['co'], reason: 'CO has no identity-verification screen' },
  { key: 'offBoarding:title', states: ['co'], reason: 'CO has no off-boarding screen' },
  { key: 'offBoarding:body1', states: ['co'], reason: 'CO has no off-boarding screen' },
  { key: 'offBoarding:action', states: ['co'], reason: 'CO has no off-boarding screen' },
  {
    key: 'offBoarding:docVerificationFailedTitle',
    states: ['co'],
    reason: 'CO has no document-verification failure screen'
  },
  {
    key: 'offBoarding:docVerificationFailedBody',
    states: ['co'],
    reason: 'CO has no document-verification failure screen'
  },

  // Colorado-only step-up screens. Each is gated on `getState() === 'co'` or
  // sits on the OIDC path, which DC never enters.
  { key: 'stepUpDisclaimer:title', states: ['dc'], reason: 'IalGuard renders only for CO' },
  { key: 'stepUpDisclaimer:body', states: ['dc'], reason: 'IalGuard renders only for CO' },
  { key: 'stepUpDisclaimer:action', states: ['dc'], reason: 'IalGuard renders only for CO' },
  { key: 'step-upProcessing:title', states: ['dc'], reason: 'CO-only loading interstitial' },
  { key: 'step-upProcessing:body', states: ['dc'], reason: 'CO-only loading interstitial' },
  { key: 'stepUpFailure:title', states: ['dc'], reason: 'DC has no OIDC callback to fail' },
  { key: 'stepUpFailure:body', states: ['dc'], reason: 'DC has no OIDC callback to fail' },

  // The email-code sign-in is DC-only; Colorado signs in through myColorado.
  { key: 'login:body', states: ['co'], reason: 'DC-only sign-in page branch' },
  { key: 'login:logInDisclaimerBody2', states: ['co'], reason: 'DC-only sign-in page branch' },
  { key: 'login:labelEmail', states: ['co'], reason: 'DC-only email form' },
  { key: 'login:verifyTitle', states: ['co'], reason: 'DC-only code screen' },
  { key: 'login:verifyLabelCode', states: ['co'], reason: 'DC-only code screen' },
  { key: 'login:verifyActionConfirm', states: ['co'], reason: 'DC-only code screen' },
  { key: 'login:verifyActionResend', states: ['co'], reason: 'DC-only code screen' },

  // COLoginPage renders only for Colorado.
  { key: 'login:logInDisclaimerBody1', states: ['dc'], reason: 'CO-only sign-in page' },
  { key: 'login:logInDisclaimerBody3', states: ['dc'], reason: 'CO-only sign-in page' },

  // Copy one state authored and the other deliberately does not show.
  { key: 'common:co-loadedCardHelper', states: ['co'], reason: 'DC-only co-loaded helper text' },
  { key: 'common:helperStreetAddress', states: ['co'], reason: 'DC-only address hint' },
  { key: 'common:titleFaqs', states: ['dc'], reason: 'CO-only help section heading' },
  {
    key: 'confirmInfo:notFoundActionHelp',
    states: ['co'],
    reason: 'DC-only address-not-found help'
  },
  {
    key: 'confirmInfo:notFoundContinue',
    states: ['dc'],
    reason: 'CO-only address-not-found action'
  },
  {
    key: 'dashboard:applicationsTableHeadingDateSubmitted',
    states: ['co'],
    reason: 'CO data has no application submitted date; show_application_date is off'
  },
  {
    key: 'dashboard:applicationsTableHeadingNumber',
    states: ['dc'],
    reason: 'CO-only applications column'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateTitle',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateBody1',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateBody2',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateBody3',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateAction2',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'dashboard:coLoadedAddressUpdateAction3',
    states: ['co'],
    reason: 'DC-only co-loaded address page'
  },
  {
    key: 'optionalId:cardNumber',
    states: ['dc'],
    reason: 'empty for DC; only CO shows a card number (see ConfirmRequest.content.test.ts)'
  },
  { key: 'result:replaceCardBody1', states: ['co'], reason: 'DC-only card information page' },
  { key: 'result:replaceCardBody2', states: ['co'], reason: 'DC-only card information page' },
  { key: 'result:replaceCardBody3', states: ['co'], reason: 'DC-only card information page' },
  { key: 'result:replaceCardBody4', states: ['co'], reason: 'DC-only card information page' },

  // Latent: no state has the key, and the branch that renders it is currently
  // unreachable. These become visible the moment the surrounding condition
  // changes, which is exactly what this guard is for.
  {
    key: 'dashboard:sectionEnrolledChildrenAction',
    states: ['dc', 'co'],
    reason:
      'renders only when an apply destination is configured; none is, since applications closed'
  },
  {
    key: 'login:callbackSigningIn',
    states: ['dc', 'co'],
    reason: 'non-CO branch of the OIDC callback, and only CO uses OIDC'
  },
  {
    key: 'checker:closed.title',
    states: ['co'],
    reason: 'season-closed route is unreferenced in code and absent from the deployed build'
  },
  {
    key: 'checker:closed.body',
    states: ['co'],
    reason: 'season-closed route is unreferenced in code and absent from the deployed build'
  },
  {
    key: 'personalInfo:schoolLabel',
    states: ['co'],
    reason: 'school field is hidden behind NEXT_PUBLIC_SHOW_SCHOOL_FIELD'
  },
  {
    key: 'personalInfo:schoolSelectPlaceholder',
    states: ['co'],
    reason: 'school field is hidden behind NEXT_PUBLIC_SHOW_SCHOOL_FIELD'
  },
  {
    key: 'common:schoolLoading',
    states: ['co'],
    reason: 'school field is hidden behind NEXT_PUBLIC_SHOW_SCHOOL_FIELD'
  },
  {
    key: 'common:schoolError',
    states: ['co'],
    reason: 'school field is hidden behind NEXT_PUBLIC_SHOW_SCHOOL_FIELD'
  }
]

interface Unresolved {
  id: string
  state: string
  languages: string[]
  sites: string[]
}

interface Coverage {
  unresolved: Unresolved[]
  /** Every `namespace:key` the app's code asks for, resolved or not. */
  referenced: Set<string>
}

function collectCoverage(app: AppName): Coverage {
  const calls = extractTranslationCalls(sourceRoots(app).flatMap((root) => walkSource(root)))
  const bundles = loadBundles(app)
  const found = new Map<string, Unresolved>()
  const referenced = new Set<string>()

  const identify = (call: TranslationCall) =>
    `${call.namespaces.filter(Boolean).join('|')}:${call.key}`

  for (const call of calls) {
    // Renders English rather than the key; a different problem.
    if (call.hasDefault) continue
    // Namespace chosen at runtime; existence cannot be decided statically.
    if (call.namespaces.includes(null)) continue
    referenced.add(identify(call))

    for (const state of APP_STATES[app]) {
      for (const lang of Object.keys(bundles[state] ?? {})) {
        if (resolves(bundles, state, lang, call)) continue
        const id = identify(call)
        const mapKey = `${id}|${state}`
        if (!found.has(mapKey)) {
          found.set(mapKey, { id, state, languages: [], sites: [] })
        }
        const entry = found.get(mapKey)!
        if (!entry.languages.includes(lang)) entry.languages.push(lang)
        const site = `${call.file}:${call.line}`
        if (!entry.sites.includes(site)) entry.sites.push(site)
      }
    }
  }

  return { unresolved: [...found.values()], referenced }
}

const isExempt = (u: Unresolved) => EXEMPT.some((e) => e.key === u.id && e.states.includes(u.state))

describe.each(Object.keys(APP_STATES) as AppName[])('%s: translation keys resolve', (app) => {
  const { unresolved, referenced } = collectCoverage(app)

  it('has no key that would render as its own name', () => {
    const offenders = unresolved.filter((u) => !isExempt(u))
    const detail = offenders
      .map(
        (u) =>
          `  ${u.id} missing for ${u.state}/${u.languages.sort().join(',')}\n      ${u.sites[0]}`
      )
      .join('\n')

    expect(
      offenders,
      offenders.length
        ? `\n\nThese keys are requested by the code but exist in no bundle, so i18next will\n` +
            `render the key name where copy belongs:\n\n${detail}\n\n` +
            `Either add the row to the state's content sheet and re-run \`pnpm copy:generate\`,\n` +
            `or, if the state genuinely never reaches that screen, add an entry to EXEMPT in\n` +
            `this file explaining why.\n`
        : undefined
    ).toEqual([])
  })

  it('has no stale exemption', () => {
    const live = new Set(unresolved.map((u) => `${u.id}|${u.state}`))
    const reportable = staleExemptions(EXEMPT, referenced, live, APP_STATES[app])

    expect(
      reportable.map((e) => e.key),
      reportable.length
        ? `\n\nThese exemptions no longer match anything: the content now exists, so the\n` +
            `delete the entry from EXEMPT.\n`
        : undefined
    ).toEqual([])
  })
})
