'use client'

import { useRouter } from 'next/navigation'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import {
  Alert,
  Button,
  InputField,
  ProcessingFieldset,
  ProcessingIndicator
} from '@sebt/design-system'

import { RequestOtpRequestSchema, useRequestOtp } from '../../api'

export function LoginForm() {
  const router = useRouter()
  const { t, i18n } = useTranslation('common')
  const { t: tLogin } = useTranslation('login')
  const { t: tValidation } = useTranslation('validation')
  const [email, setEmail] = useState('')
  // Error state holds `validation` namespace keys (not resolved strings) so the messages
  // re-translate at render time when the user switches language (DC-454).
  const [fieldErrorKey, setFieldErrorKey] = useState<string | null>(null)
  const [submitErrorKey, setSubmitErrorKey] = useState<string | null>(null)

  const requestOtp = useRequestOtp()
  const { trackEvent } = useDataLayer()

  // Returns a `validation` namespace key (resolved at render), or null when valid.
  function validateEmail(value: string): string | null {
    if (!value.trim()) {
      return 'required'
    }
    const result = RequestOtpRequestSchema.shape.email.safeParse(value)
    if (!result.success) {
      return 'enterEmail'
    }
    return null
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitErrorKey(null)

    const errorKey = validateEmail(email)
    if (errorKey) {
      setFieldErrorKey(errorKey)
      return
    }
    setFieldErrorKey(null)

    trackEvent(AnalyticsEvents.OTP_REQUEST)

    try {
      await requestOtp.mutateAsync({ email, locale: i18n.language })
      sessionStorage.setItem('otp_email', email)
      router.push('/login/verify')
    } catch {
      // Map any failure to a key so the banner follows the active language. Backend
      // messages are English-only and would freeze on a language switch.
      setSubmitErrorKey('globalInternalError')
    }
  }

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

      <ProcessingFieldset
        isProcessing={requestOtp.isPending}
        legend={tLogin('labelEmail')}
        legendHidden
      >
        <InputField
          label={tLogin('labelEmail')}
          type="email"
          name="email"
          autoComplete="email"
          isRequired
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          onBlur={() => setFieldErrorKey(validateEmail(email))}
          className="maxw-full"
          {...(fieldErrorKey ? { error: tValidation(fieldErrorKey) } : {})}
        />
      </ProcessingFieldset>

      <div className="margin-top-3 display-flex flex-row flex-align-center gap-2">
        <Button
          type="submit"
          isLoading={requestOtp.isPending}
          data-analytics-cta="login_cta"
        >
          {t('continue')}
        </Button>
        <ProcessingIndicator
          isProcessing={requestOtp.isPending}
          label={t('processing')}
        />
      </div>
    </form>
  )
}
