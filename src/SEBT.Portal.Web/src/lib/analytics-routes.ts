import type { RoutePageContextMap } from '@sebt/analytics'

export const portalRoutes: RoutePageContextMap = {
  '/login': { name: 'Login', flow: 'auth', step: 'login' },
  '/login/verify': { name: 'Verify OTP', flow: 'auth', step: 'verify_otp' },
  '/login/id-proofing': { name: 'ID Proofing', flow: 'auth', step: 'id_proofing' },
  '/login/id-proofing/doc-verify': { name: 'Document Verify', flow: 'auth', step: 'doc_verify' },
  '/login/id-proofing/off-boarding': { name: 'Off-Boarding', flow: 'auth', step: 'off_boarding' },
  '/callback': { name: 'Auth Callback', flow: 'auth', step: 'callback' },
  '/dashboard': { name: 'Dashboard', flow: 'dashboard', step: 'dashboard' },
  '/profile/address': { name: 'Address Update', flow: 'address_update', step: 'address_form' },
  '/profile/address/replacement-cards': {
    name: 'Replacement Cards',
    flow: 'address_update',
    step: 'replacement_cards'
  },
  '/profile/address/replacement-cards/select': {
    name: 'Select Replacement Card',
    flow: 'address_update',
    step: 'replacement_cards_select'
  },
  '/profile/address/info': { name: 'Address Info', flow: 'address_update', step: 'address_info' }
}
