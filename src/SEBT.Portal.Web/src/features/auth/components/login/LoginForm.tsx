'use client'

import { useRouter } from 'next/navigation'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { ApiError } from '@/api/client'
import { Alert, Button, InputField } from '@/components/ui'

import { RequestOtpRequestSchema, useRequestOtp } from '../../api'

export function LoginForm() {
  const router = useRouter()
  const { t } = useTranslation('common')
  const { t: tLogin } = useTranslation('login')
  const { t: tValidation } = useTranslation('validation')
  const [email, setEmail] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const requestOtp = useRequestOtp()

  function validateEmail(value: string): string | null {
    if (!value.trim()) {
      return tValidation('required')
    }
    const result = RequestOtpRequestSchema.shape.email.safeParse(value)
    if (!result.success) {
      return tValidation('enterEmail')
    }
    return null
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)

    const error = validateEmail(email)
    if (error) {
      setFieldError(error)
      return
    }
    setFieldError(null)

    try {
      await requestOtp.mutateAsync({ email })
      sessionStorage.setItem('otp_email', email)
      router.push('/login/verify')
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message)
      } else {
        setSubmitError(tValidation('globalInternalError'))
      }
    }
  }

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

      <InputField
        label={tLogin('labelEmail')}
        type="email"
        name="email"
        autoComplete="email"
        isRequired
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        onBlur={() => setFieldError(validateEmail(email))}
        disabled={requestOtp.isPending}
        className="maxw-full"
        {...(fieldError ? { error: fieldError } : {})}
      />

      <Button
        type="submit"
        isLoading={requestOtp.isPending}
        loadingText={`${t('continue')}...`}
        className="margin-top-3"
      >
        {t('continue')}
      </Button>
    </form>
  )
}
