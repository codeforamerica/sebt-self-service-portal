/**
 * Analytics event name constants from the SEBT Data Layer Dictionary.
 * All events use Analytics scope.
 */

// Global
export const PAGE_LOAD = 'page_load'
export const CTA_CLICK = 'cta_click'

// Authentication
export const OTP_REQUEST = 'otp_request'
export const OTP_CHALLENGE = 'otp_challenge'
export const OTP_RESULT = 'otp_result'
export const OIDC_START = 'oidc_start'

// ID Proofing
export const IDV_PRIMARY_START = 'idv_primary_start'
export const IDV_PRIMARY_RESULT = 'idv_primary_result'
export const DOCV_START = 'docv_start'
export const DOCV_UPLOAD = 'docv_upload'
export const DOCV_RESULT = 'docv_result'
export const DOCV_RESUBMIT = 'docv_resubmit'
export const IDV_FINAL_RESULT = 'idv_final_result'

// Benefits Dashboard
export const HOUSEHOLD_RESULT = 'household_result'

// Self-Service Address Update & Replacement Card

/** Fired when the user enters the address update form. */
export const ADDRESS_UPDATE_START = 'address_update_start'
/** Fired when the address update API call completes. Carries `address_update_status` (page). */
export const ADDRESS_UPDATE_SUBMIT = 'address_update_submit'
/** Fired when the address update API call fails. Carries `error_code` (page). */
export const ADDRESS_UPDATE_ERROR = 'address_update_error'
/**
 * Fired when local (client-side) form validation blocks the address-update submit before any
 * backend request — so failures like an over-length street address are still measured. Carries
 * `error_code` and `field_name` (page); `flow`/`step` ride along from the page context.
 */
export const ADDRESS_UPDATE_VALIDATION_ERROR = 'address_update_validation_error'
/** Fired when the user enters the card replacement flow. */
export const CARD_REPLACEMENT_START = 'card_replacement_start'
/** Fired when the card replacement API call completes. Carries `card_replacement_status` (page). */
export const CARD_REPLACEMENT_SUBMIT = 'card_replacement_submit'
/** Fired when the card replacement API call fails. Carries `error_code` (page). */
export const CARD_REPLACEMENT_ERROR = 'card_replacement_error'

// Enrollment Checker — per the SEBT Data Layer Dictionary, each event carries
// the analytics-scoped page + user context the data layer has at fire time
// (merged automatically by `_trackEvent`). Privacy: none of these payloads
// include child PII (firstName, lastName, DOB, school) — verified by tests.

/** Fired when the user lands on the child-form page. Carries `name` + `application` from page context. */
export const ENROLLMENT_CHECK_START = 'enrollment_check_start'
/** Fired when the eligibility result page loads. Carries `enrollment_match_type` (page) + `sebt_eligible` (user). */
export const ENROLLMENT_CHECK_RESULT = 'enrollment_check_result'
/** Fired when the submission API call fails. Carries `error_code` (page). */
export const ENROLLMENT_CHECK_ERROR = 'enrollment_check_error'
