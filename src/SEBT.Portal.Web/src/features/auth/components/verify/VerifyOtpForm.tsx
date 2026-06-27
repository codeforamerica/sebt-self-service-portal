'use client'

import { useRouter } from 'next/navigation'
import { useCallback, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useCountdown } from 'usehooks-ts'

import { ApiError } from '@/api/client'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, Button, InputField, TextLink } from '@sebt/design-system'

import { needsIdProofingFlowAfterOtp } from '@/lib/idProofingStatus'

import { useRequestOtp, useValidateOtp, ValidateOtpRequestSchema } from '../../api'
import { useAuth } from '../../context'

const RESEND_COOLDOWN_SECONDS = 30

interface VerifyOtpFormProps {
  email: string
  contactLink: string
}

export function VerifyOtpForm({ email, contactLink }: VerifyOtpFormProps) {
  const router = useRouter()
  const { login } = useAuth()
  const { t: tLogin, i18n } = useTranslation('login')
  const { t: tValidation } = useTranslation('validation')

  const [otp, setOtp] = useState('')
  // Error/status state holds `validation` namespace keys (not resolved strings) so the
  // messages re-translate at render time when the user switches language (DC-454).
  const [fieldErrorKey, setFieldErrorKey] = useState<string | null>(null)
  const [submitErrorKey, setSubmitErrorKey] = useState<string | null>(null)
  const [successMessageKey, setSuccessMessageKey] = useState<string | null>(null)

  const [count, { startCountdown, resetCountdown }] = useCountdown({
    countStart: RESEND_COOLDOWN_SECONDS,
    countStop: 0,
    intervalMs: 1000
  })

  const validateOtp = useValidateOtp()
  const requestOtp = useRequestOtp()
  const { setPageData, setUserData, trackEvent } = useDataLayer()

  // Returns a `validation` namespace key (resolved at render), or null when valid.
  const validateCode = useCallback((value: string): string | null => {
    if (!value.trim()) {
      return 'required'
    }
    const result = ValidateOtpRequestSchema.shape.otp.safeParse(value)
    if (!result.success) {
      return 'otpInvalid'
    }
    return null
  }, [])

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitErrorKey(null)
    setSuccessMessageKey(null)

    const errorKey = validateCode(otp)
    if (errorKey) {
      setFieldErrorKey(errorKey)
      return
    }
    setFieldErrorKey(null)

    trackEvent(AnalyticsEvents.OTP_CHALLENGE)

    try {
      await validateOtp.mutateAsync({ email, otp })
      setPageData('otp_status', 'success')
      setUserData('authenticated', true, ['default', 'analytics'])
      // Backend set the HttpOnly session cookie; refresh the context from /auth/status.
      const newSession = await login()
      if (!newSession) {
        setPageData('otp_status', 'error')
        setUserData('authenticated', false, ['default', 'analytics'])
        trackEvent(AnalyticsEvents.OTP_RESULT)
        setSubmitErrorKey('globalInternalError')
        return
      }
      trackEvent(AnalyticsEvents.OTP_RESULT)
      sessionStorage.removeItem('otp_email')
      // Only Completed — InProgress / Failed / Expired / missing claim route to proofing flow.
      const needsIdProofing = needsIdProofingFlowAfterOtp(newSession.idProofingStatus)
      router.push(needsIdProofing ? '/login/id-proofing' : '/dashboard')
    } catch (err) {
      setPageData('otp_status', 'error')
      trackEvent(AnalyticsEvents.OTP_RESULT)
      // A 401 means the code was wrong or expired — both resolve to the actionable
      // "enter a valid code, request a new one" copy. Other failures get the generic
      // internal-error message. Both are i18n keys resolved at render so they follow a
      // language switch (DC-454); raw backend messages are English-only and would freeze.
      setSubmitErrorKey(
        err instanceof ApiError && (err.status === 401 || err.status === 400)
          ? 'otpInvalid'
          : 'globalInternalError'
      )
    }
  }

  // Countdown is active when count > 0 and has been started (not at initial value before first start)
  const [hasStartedCountdown, setHasStartedCountdown] = useState(false)
  const isCountdownActive = hasStartedCountdown && count > 0

  async function handleResend() {
    if (isCountdownActive) return

    setSubmitErrorKey(null)
    setSuccessMessageKey(null)

    trackEvent(AnalyticsEvents.OTP_REQUEST)

    try {
      await requestOtp.mutateAsync({ email, locale: i18n.language })
      // validation:newCode carries the intended "A new code has been sent" copy in every
      // language; resolved at render so it follows a language switch (DC-454). The
      // login:codeSentSuccess row in the content sheet is malformed — tracked separately.
      setSuccessMessageKey('newCode')
      resetCountdown()
      startCountdown()
      setHasStartedCountdown(true)
    } catch {
      setSubmitErrorKey('globalInternalError')
    }
  }

  const isSubmitting = validateOtp.isPending
  const isResending = requestOtp.isPending
  const resendDisabled = isCountdownActive || isResending || isSubmitting

  return (
    <form
      className="usa-form maxw-full text-left"
      onSubmit={handleSubmit}
    >
      {submitErrorKey && (
        <Alert
          variant="error"
          slim
          className="margin-bottom-2"
        >
          {tValidation(submitErrorKey)}
        </Alert>
      )}

      {successMessageKey && (
        <Alert
          variant="success"
          slim
          className="margin-bottom-2"
        >
          {tValidation(successMessageKey)}
        </Alert>
      )}

      <InputField
        label={tLogin('verifyLabelCode')}
        type="text"
        inputMode="numeric"
        name="otp"
        autoComplete="one-time-code"
        isRequired
        maxLength={6}
        value={otp}
        onChange={(e) => setOtp(e.target.value)}
        onBlur={() => setFieldErrorKey(validateCode(otp))}
        disabled={isSubmitting}
        className="maxw-full"
        {...(fieldErrorKey ? { error: tValidation(fieldErrorKey) } : {})}
      />

      {/* Confirm button */}
      <Button
        type="submit"
        isLoading={isSubmitting}
        loadingText={`${tLogin('verifyActionConfirm')}...`}
        className="margin-top-3 display-block"
      >
        {tLogin('verifyActionConfirm')}
      </Button>

      {/* Resend button */}
      <Button
        type="button"
        variant="outline"
        onClick={handleResend}
        disabled={resendDisabled}
        isLoading={isResending}
        className="margin-top-3 display-block"
      >
        {isCountdownActive
          ? `${tLogin('verifyActionResend')} (${count}s)`
          : tLogin('verifyActionResend')}
      </Button>

      <p className="margin-top-4 font-sans-sm">
        <TextLink
          href={contactLink}
          target="_blank"
          rel="noopener noreferrer"
        >
          {tLogin('logInDisclaimerBody2')}
        </TextLink>
      </p>
    </form>
  )
}
