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

// ID Proofing
export const IDV_PRIMARY_START = 'idv_primary_start'
export const IDV_PRIMARY_RESULT = 'idv_primary_result'
export const DOCV_START = 'docv_start'
export const DOCV_UPLOAD = 'docv_upload'
export const DOCV_RESULT = 'docv_result'
export const IDV_FINAL_RESULT = 'idv_final_result'

// Benefits Dashboard
export const HOUSEHOLD_RESULT = 'household_result'

// Self-Service Address Update & Replacement Card
export const ADDRESS_UPDATE_START = 'address_update_start'
export const ADDRESS_UPDATE_SUBMIT = 'address_update_submit'
export const ADDRESS_UPDATE_ERROR = 'address_update_error'
export const CARD_REPLACEMENT_START = 'card_replacement_start'
export const CARD_REPLACEMENT_SUBMIT = 'card_replacement_submit'
export const CARD_REPLACEMENT_ERROR = 'card_replacement_error'

// Enrollment Checker
export const ENROLLMENT_CHECK_START = 'enrollment_check_start'
export const ENROLLMENT_CHECK_RESULT = 'enrollment_check_result'
export const ENROLLMENT_CHECK_ERROR = 'enrollment_check_error'
