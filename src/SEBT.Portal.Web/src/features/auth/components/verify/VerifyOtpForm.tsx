'use client'

import { useRouter } from 'next/navigation'
import { useCallback, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useCountdown } from 'usehooks-ts'

import { ApiError } from '@/api/client'
import { Alert, Button, InputField, TextLink } from '@/components/ui'

import { useRequestOtp, useValidateOtp, ValidateOtpRequestSchema } from '../../api'

const RESEND_COOLDOWN_SECONDS = 30

interface VerifyOtpFormProps {
  email: string
  contactLink: string
}

export function VerifyOtpForm({ email, contactLink }: VerifyOtpFormProps) {
  const router = useRouter()
  const { t: tLogin } = useTranslation('login')
  const { t: tValidation } = useTranslation('validation')

  const [otp, setOtp] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  const [count, { startCountdown, resetCountdown }] = useCountdown({
    countStart: RESEND_COOLDOWN_SECONDS,
    countStop: 0,
    intervalMs: 1000
  })

  const validateOtp = useValidateOtp()
  const requestOtp = useRequestOtp()

  const validateCode = useCallback(
    (value: string): string | null => {
      if (!value.trim()) {
        return tValidation('required')
      }
      const result = ValidateOtpRequestSchema.shape.otp.safeParse(value)
      if (!result.success) {
        return tValidation('otpInvalid')
      }
      return null
    },
    [tValidation]
  )

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)
    setSuccessMessage(null)

    const error = validateCode(otp)
    if (error) {
      setFieldError(error)
      return
    }
    setFieldError(null)

    try {
      await validateOtp.mutateAsync({ email, otp })
      // Clear the stored email
      sessionStorage.removeItem('otp_email')
      // TODO: Store token in auth context and redirect to dashboard
      router.push('/')
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message)
      } else {
        // TODO: Add translation key for unexpected error (e.g., GLOBAL - Error Unexpected)
        setSubmitError('Something went wrong. Please try again.')
      }
    }
  }

  // Countdown is active when count > 0 and has been started (not at initial value before first start)
  const [hasStartedCountdown, setHasStartedCountdown] = useState(false)
  const isCountdownActive = hasStartedCountdown && count > 0

  async function handleResend() {
    if (isCountdownActive) return

    setSubmitError(null)
    setSuccessMessage(null)

    try {
      await requestOtp.mutateAsync({ email })
      // TODO: Add translation key for code sent success (e.g., S8 - OTP Confirm - Code Sent Success)
      setSuccessMessage('A new code has been sent to your email.')
      resetCountdown()
      startCountdown()
      setHasStartedCountdown(true)
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message)
      } else {
        // TODO: Add translation key for unexpected error (e.g., GLOBAL - Error Unexpected)
        setSubmitError('Something went wrong. Please try again.')
      }
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
      {submitError && (
        <Alert
          variant="error"
          slim
          className="margin-bottom-2"
        >
          {submitError}
        </Alert>
      )}

      {successMessage && (
        <Alert
          variant="success"
          slim
          className="margin-bottom-2"
        >
          {successMessage}
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
        onBlur={() => setFieldError(validateCode(otp))}
        disabled={isSubmitting}
        className="maxw-full"
        {...(fieldError ? { error: fieldError } : {})}
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
